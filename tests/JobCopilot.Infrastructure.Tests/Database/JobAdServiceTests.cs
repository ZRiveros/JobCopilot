using JobCopilot.Application.JobAds;
using JobCopilot.Domain.JobAds;

namespace JobCopilot.Infrastructure.Tests.Database;

[Collection(DatabaseCollection.Name)]
public class JobAdServiceTests(PostgresFixture fixture)
{
    private static async Task<List<string>> HeadlinesAsync(TestApp app, JobAdFilter filter) =>
        (await app.Ads.SearchAsync(filter)).Items.Select(ad => ad.Headline).ToList();

    [Fact]
    public async Task Search_Default_RanksByScoreAndLeavesOutHiddenAndExpired()
    {
        await using var app = await TestApp.CreateAsync(fixture);
        await app.SeedAdAsync("1", score: 40, headline: "Medel");
        await app.SeedAdAsync("2", score: 90, headline: "Bäst");
        await app.SeedAdAsync("3", score: 95, headline: "Dold", hidden: true);
        await app.SeedAdAsync("4", score: 99, headline: "Utgången", deadline: TestApp.Now.AddDays(-1));
        await app.SeedAdAsync("5", score: 10, headline: "Sämst", deadline: TestApp.Now.AddDays(3));

        var result = await app.Ads.SearchAsync(new JobAdFilter());

        Assert.Equal(["Bäst", "Medel", "Sämst"], result.Items.Select(ad => ad.Headline));
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task Search_Views()
    {
        await using var app = await TestApp.CreateAsync(fixture);
        var saved = await app.SeedAdAsync("1", score: 30, headline: "Sparad");
        await app.SeedAdAsync("2", score: 20, headline: "Dold", hidden: true);
        await app.SeedAdAsync("3", score: 10, headline: "Vanlig");
        await app.Applications.SaveAdAsync(saved.Id);

        Assert.Equal(["Sparad", "Vanlig"], await HeadlinesAsync(app, new() { View = JobAdView.Active }));
        Assert.Equal(["Sparad"], await HeadlinesAsync(app, new() { View = JobAdView.Saved }));
        Assert.Equal(["Dold"], await HeadlinesAsync(app, new() { View = JobAdView.Hidden }));
        Assert.Equal(["Sparad", "Dold", "Vanlig"], await HeadlinesAsync(app, new() { View = JobAdView.All }));
    }

    [Fact]
    public async Task Search_SavedAd_CarriesItsBoardStatus()
    {
        await using var app = await TestApp.CreateAsync(fixture);
        var ad = await app.SeedAdAsync("1");
        var applicationId = await app.Applications.SaveAdAsync(ad.Id);

        var summary = Assert.Single((await app.Ads.SearchAsync(new())).Items);

        Assert.True(summary.IsSaved);
        Assert.Equal(applicationId, summary.ApplicationId);
        Assert.Equal(Domain.Applications.ApplicationStatus.Saved, summary.ApplicationStatus);
    }

    [Fact]
    public async Task Search_Filters()
    {
        await using var app = await TestApp.CreateAsync(fixture);
        await app.SeedAdAsync("1", score: 80, headline: "Blazor-utvecklare", description: "Vi bygger i Blazor.",
            municipality: "Norrköping", matched: ["Blazor"], missing: ["Docker"]);
        await app.SeedAdAsync("2", score: 60, headline: "Backend", description: "API:er åt BLAZOR-teamet.",
            municipality: "Linköping", matched: ["C#"], missing: ["Azure"]);
        await app.SeedAdAsync("3", score: 20, headline: "Javautvecklare", description: "Spring Boot.",
            municipality: "Linköping", matched: [], missing: ["Java", "Docker"]);

        Assert.Equal(["Blazor-utvecklare", "Backend"], await HeadlinesAsync(app, new() { MinScore = 50 }));
        Assert.Equal(["Backend", "Javautvecklare"], await HeadlinesAsync(app, new() { Municipality = "Linköping" }));
        Assert.Equal(["Blazor-utvecklare", "Backend"], await HeadlinesAsync(app, new() { Search = "  blazor " }));
        Assert.Equal(["Blazor-utvecklare", "Javautvecklare"], await HeadlinesAsync(app, new() { Skill = "Docker" }));
        Assert.Equal(["Blazor-utvecklare"], await HeadlinesAsync(app, new() { Skill = "Blazor" }));
        Assert.Empty(await HeadlinesAsync(app, new() { Search = "finns inte" }));
    }

    [Fact]
    public async Task Search_Sorting()
    {
        await using var app = await TestApp.CreateAsync(fixture);
        await app.SeedAdAsync("1", score: 10, headline: "A", publishedAt: TestApp.Now.AddDays(-1), deadline: TestApp.Now.AddDays(20));
        await app.SeedAdAsync("2", score: 30, headline: "B", publishedAt: TestApp.Now.AddDays(-5), deadline: TestApp.Now.AddDays(2));
        await app.SeedAdAsync("3", score: 20, headline: "C", publishedAt: TestApp.Now.AddDays(-3), deadline: null);

        Assert.Equal(["B", "C", "A"], await HeadlinesAsync(app, new() { Sort = JobAdSort.BestMatch }));
        Assert.Equal(["A", "C", "B"], await HeadlinesAsync(app, new() { Sort = JobAdSort.Newest }));
        Assert.Equal(["B", "A", "C"], await HeadlinesAsync(app, new() { Sort = JobAdSort.Deadline }));
    }

    [Fact]
    public async Task Search_Pages()
    {
        await using var app = await TestApp.CreateAsync(fixture);
        for (var i = 1; i <= 5; i++)
        {
            await app.SeedAdAsync(i.ToString(System.Globalization.CultureInfo.InvariantCulture), score: 100 - i, headline: $"Annons {i}");
        }

        var page = await app.Ads.SearchAsync(new JobAdFilter { Skip = 2, Take = 2 });

        Assert.Equal(["Annons 3", "Annons 4"], page.Items.Select(ad => ad.Headline));
        Assert.Equal(5, page.TotalCount);
        Assert.True(page.HasMore(alreadyLoaded: 4));
    }

    [Fact]
    public async Task SetHidden_TogglesVisibility()
    {
        await using var app = await TestApp.CreateAsync(fixture);
        var ad = await app.SeedAdAsync("1");

        await app.Ads.SetHiddenAsync(ad.Id, hidden: true);
        Assert.Empty((await app.Ads.SearchAsync(new())).Items);

        await app.Ads.SetHiddenAsync(ad.Id, hidden: false);
        Assert.Single((await app.Ads.SearchAsync(new())).Items);
    }

    [Fact]
    public async Task AddManual_StoresAndScoresTheAd()
    {
        await using var app = await TestApp.CreateAsync(fixture);
        await app.Profiles.SaveAsync(TestApp.Profile("C#", "Blazor"));

        var id = await app.Ads.AddManualAsync(new ManualJobAdInput
        {
            Headline = "Blazor-utvecklare",
            EmployerName = "Hittad på LinkedIn AB",
            Municipality = "Norrköping",
            Url = "https://example.com/jobb",
            Description = "C# och Docker.",
        });

        var ad = await app.Ads.GetAsync(id);
        Assert.NotNull(ad);
        Assert.Equal(JobAd.ManualSource, ad.Source);
        Assert.Equal(["Blazor", "C#"], ad.MatchedSkills);
        Assert.Equal(["Docker"], ad.MissingSkills);
        Assert.Equal("https://example.com/jobb", ad.ApplyUrl);
    }

    [Fact]
    public async Task GetMunicipalities_DistinctAndSorted()
    {
        await using var app = await TestApp.CreateAsync(fixture);
        await app.SeedAdAsync("1", municipality: "Norrköping");
        await app.SeedAdAsync("2", municipality: "Linköping");
        await app.SeedAdAsync("3", municipality: "Norrköping");
        await app.SeedAdAsync("4", municipality: null);
        await app.SeedAdAsync("5", municipality: "Motala", hidden: true);

        Assert.Equal(["Linköping", "Norrköping"], await app.Ads.GetMunicipalitiesAsync());
    }

    [Fact]
    public async Task GetSkillGaps_CountsAcrossRelevantAdsOnly()
    {
        await using var app = await TestApp.CreateAsync(fixture);
        await app.SeedAdAsync("1", matched: ["C#"], missing: ["Docker", "Azure"]);
        await app.SeedAdAsync("2", matched: ["C#"], missing: ["Docker"]);
        await app.SeedAdAsync("3", matched: [], missing: ["Kubernetes"], hidden: true);
        await app.SeedAdAsync("4", matched: [], missing: ["Kubernetes"], deadline: TestApp.Now.AddDays(-1));

        var report = await app.Ads.GetSkillGapsAsync();

        Assert.Equal(2, report.AdCount);
        Assert.Equal([new SkillCount("Docker", 2, 1.0), new SkillCount("Azure", 1, 0.5)], report.Missing);
        Assert.Equal([new SkillCount("C#", 2, 1.0)], report.Matched);
    }
}
