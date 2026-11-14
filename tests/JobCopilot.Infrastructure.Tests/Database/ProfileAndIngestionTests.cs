using JobCopilot.Domain.JobAds;
using JobCopilot.Domain.Profiles;
using Microsoft.EntityFrameworkCore;

namespace JobCopilot.Infrastructure.Tests.Database;

[Collection(DatabaseCollection.Name)]
public class ProfilePersistenceTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Get_BeforeAnythingIsSaved_ReturnsEmptyProfile()
    {
        await using var app = await TestApp.CreateAsync(fixture);

        var profile = await app.Profiles.GetAsync();

        Assert.True(profile.IsEmpty);
    }

    [Fact]
    public async Task Save_RoundTripsEverySection()
    {
        await using var app = await TestApp.CreateAsync(fixture);
        var profile = new MasterProfile
        {
            FullName = "Anna Andersson",
            Headline = "Junior .NET-utvecklare",
            Email = "anna@example.com",
            Location = "Norrköping",
            Summary = "Gillar backend.",
            Strengths = ["Noggrann", "Nyfiken"],
            Skills = [new Skill { Name = "C#", Category = "Backend", Level = SkillLevel.Advanced }],
            Experiences =
            [
                new Experience
                {
                    Title = "Systemutvecklare (LIA)",
                    Company = "Exempel AB",
                    StartDate = new DateOnly(2025, 1, 1),
                    EndDate = null,
                    Description = "Rad ett\nRad två med åäö",
                    Technologies = ["C#", "SQL Server"],
                },
            ],
            Projects = [new ProfileProject { Name = "JobCopilot", Url = "https://github.com/x/y", Technologies = ["Blazor"] }],
            Education = [new Education { School = "YH", Program = ".NET", EndDate = new DateOnly(2025, 6, 1) }],
            Languages = [new SpokenLanguage { Name = "Svenska", Level = "Modersmål" }],
        };

        await app.Profiles.SaveAsync(profile);
        var loaded = await app.Profiles.GetAsync();

        Assert.Equal("Anna Andersson", loaded.FullName);
        Assert.Equal(["Noggrann", "Nyfiken"], loaded.Strengths);
        Assert.Equal(TestApp.Now, loaded.UpdatedAt);

        var skill = Assert.Single(loaded.Skills);
        Assert.Equal(("C#", "Backend", SkillLevel.Advanced), (skill.Name, skill.Category, skill.Level));

        var experience = Assert.Single(loaded.Experiences);
        Assert.Equal("Systemutvecklare (LIA)", experience.Title);
        Assert.Equal(new DateOnly(2025, 1, 1), experience.StartDate);
        Assert.Null(experience.EndDate);
        Assert.Equal("Rad ett\nRad två med åäö", experience.Description);
        Assert.Equal(["C#", "SQL Server"], experience.Technologies);

        Assert.Equal(["Blazor"], Assert.Single(loaded.Projects).Technologies);
        Assert.Equal(new DateOnly(2025, 6, 1), Assert.Single(loaded.Education).EndDate);
        Assert.Equal("Modersmål", Assert.Single(loaded.Languages).Level);
    }

    [Fact]
    public async Task Save_SecondTime_ReplacesSectionsInsteadOfAddingARow()
    {
        await using var app = await TestApp.CreateAsync(fixture);
        await app.Profiles.SaveAsync(TestApp.Profile("C#", "SQL"));

        var edited = await app.Profiles.GetAsync();
        edited.FullName = "Anna B. Andersson";
        edited.Skills.RemoveAll(skill => skill.Name == "SQL");
        edited.Skills.Add(new Skill { Name = "Docker" });
        await app.Profiles.SaveAsync(edited);

        var loaded = await app.Profiles.GetAsync();
        Assert.Equal("Anna B. Andersson", loaded.FullName);
        Assert.Equal(["C#", "Docker"], loaded.Skills.Select(skill => skill.Name));

        await using var db = app.Database.CreateContext();
        Assert.Equal(1, await db.Profiles.CountAsync());
    }

    [Fact]
    public async Task Save_RescoresStoredAds()
    {
        await using var app = await TestApp.CreateAsync(fixture);
        app.Source.Returns(".NET", TestApp.Listing("1", description: "C#, SQL, Docker och Azure."));
        await app.Searches.SaveAsync(new() { Query = ".NET" });
        await app.Ingestion.RunAsync();

        var before = Assert.Single((await app.Ads.SearchAsync(new())).Items);
        Assert.Equal(0, before.MatchScore);

        await app.Profiles.SaveAsync(TestApp.Profile("C#", "SQL"));

        var after = Assert.Single((await app.Ads.SearchAsync(new())).Items);
        Assert.Equal(50, after.MatchScore);
        Assert.Equal(["C#", "SQL"], after.MatchedSkills);
        Assert.Equal(["Azure", "Docker"], after.MissingSkills);
    }
}

[Collection(DatabaseCollection.Name)]
public class JobAdIngestionTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Run_StoresAndScoresNewAds()
    {
        await using var app = await TestApp.CreateAsync(fixture);
        await app.Profiles.SaveAsync(TestApp.Profile("C#"));
        await app.Searches.SaveAsync(new() { Query = ".NET", MunicipalityCodes = ["0581"], RemoteOnly = true });
        app.Source.Returns(".NET", TestApp.Listing("1"), TestApp.Listing("2", headline: "Lagerarbetare", description: "Truckkort."));

        var summary = await app.Ingestion.RunAsync();

        Assert.Equal((1, 0, 2, 2), (summary.Searches, summary.FailedSearches, summary.FetchedAds, summary.NewAds));

        var ads = (await app.Ads.SearchAsync(new())).Items;
        Assert.Equal(["Utvecklare", "Lagerarbetare"], ads.Select(ad => ad.Headline));
        Assert.Equal(["C#"], ads[0].MatchedSkills);
        Assert.Equal(["Docker"], ads[0].MissingSkills);
        Assert.Equal(0, ads[1].MatchScore);

        var criteria = Assert.Single(app.Source.Calls);
        Assert.Equal((".NET", true), (criteria.Query, criteria.RemoteOnly));
        Assert.Equal(["0581"], criteria.MunicipalityCodes);

        var search = Assert.Single(await app.Searches.GetAllAsync());
        Assert.True(search.LastRunSucceeded);
        Assert.Equal((2, 2), (search.LastResultCount, search.LastNewCount));
        Assert.Equal(TestApp.Now, search.LastRunAt);
    }

    [Fact]
    public async Task Run_SameAdFoundByTwoSearches_IsStoredOnce()
    {
        await using var app = await TestApp.CreateAsync(fixture);
        await app.Searches.SaveAsync(new() { Query = ".NET" });
        await app.Searches.SaveAsync(new() { Query = "C#" });
        app.Source.Returns(".NET", TestApp.Listing("1"), TestApp.Listing("2"));
        app.Source.Returns("C#", TestApp.Listing("2"), TestApp.Listing("3"), TestApp.Listing("3"));

        var summary = await app.Ingestion.RunAsync();

        Assert.Equal(3, summary.NewAds);
        Assert.Equal(3, (await app.Ads.SearchAsync(new())).TotalCount);
    }

    [Fact]
    public async Task Run_Again_RefreshesKnownAdsAndKeepsUserState()
    {
        await using var app = await TestApp.CreateAsync(fixture);
        await app.Searches.SaveAsync(new() { Query = ".NET" });
        app.Source.Returns(".NET", TestApp.Listing("1", headline: "Gammal rubrik"));
        await app.Ingestion.RunAsync();

        var stored = Assert.Single((await app.Ads.SearchAsync(new())).Items);
        await app.Ads.SetHiddenAsync(stored.Id, hidden: true);
        var applicationId = await app.Applications.SaveAdAsync(stored.Id);

        app.Time.Advance(TimeSpan.FromHours(1));
        app.Source.Returns(".NET", TestApp.Listing("1", headline: "Ny rubrik"));
        var summary = await app.Ingestion.RunAsync();

        Assert.Equal(0, summary.NewAds);
        var ad = await app.Ads.GetAsync(stored.Id);
        Assert.NotNull(ad);
        Assert.Equal("Ny rubrik", ad.Headline);
        Assert.Equal(TestApp.Now, ad.FirstSeenAt);
        Assert.Equal(TestApp.Now.AddHours(1), ad.LastSeenAt);
        Assert.Equal(applicationId, ad.Application?.Id);
    }

    [Fact]
    public async Task Run_FailingSearch_IsRecordedAndDoesNotStopTheOthers()
    {
        await using var app = await TestApp.CreateAsync(fixture);
        await app.Searches.SaveAsync(new() { Query = "trasig" });
        await app.Searches.SaveAsync(new() { Query = ".NET" });
        app.Source.Fails("trasig");
        app.Source.Returns(".NET", TestApp.Listing("1"));

        var summary = await app.Ingestion.RunAsync();

        Assert.Equal((2, 1, 1), (summary.Searches, summary.FailedSearches, summary.NewAds));

        var searches = await app.Searches.GetAllAsync();
        var broken = searches.Single(search => search.Query == "trasig");
        Assert.False(broken.LastRunSucceeded);
        Assert.Contains("503", broken.LastError, StringComparison.Ordinal);
        Assert.True(searches.Single(search => search.Query == ".NET").LastRunSucceeded);
    }

    [Fact]
    public async Task Run_SkipsDisabledSearches()
    {
        await using var app = await TestApp.CreateAsync(fixture);
        await app.Searches.SaveAsync(new() { Query = ".NET", IsEnabled = false });

        var summary = await app.Ingestion.RunAsync();

        Assert.Equal(0, summary.Searches);
        Assert.Empty(app.Source.Calls);
    }

    [Fact]
    public async Task SeedDefaults_OnlyOnFirstRun()
    {
        await using var app = await TestApp.CreateAsync(fixture);

        await app.Searches.SeedDefaultsAsync();
        var seeded = await app.Searches.GetAllAsync();
        await app.Searches.DeleteAsync(seeded[0].Id);
        await app.Searches.SeedDefaultsAsync();

        Assert.Equal(4, seeded.Count);
        Assert.Contains(seeded, search => search.MunicipalityCodes.SequenceEqual(["0581", "0580"]));
        Assert.Contains(seeded, search => search.RemoteOnly);
        Assert.Equal(3, (await app.Searches.GetAllAsync()).Count);
    }

    [Fact]
    public async Task Database_RejectsTheSameSourceAdTwice()
    {
        await using var app = await TestApp.CreateAsync(fixture);
        await app.SeedAdAsync("1");

        await using var db = app.Database.CreateContext();
        db.JobAds.Add(JobAd.Create(JobAd.JobTechSource, TestApp.Listing("1"), TestApp.Now));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
