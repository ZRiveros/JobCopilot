using System.Text;
using JobCopilot.Application.Applications;
using JobCopilot.Application.Documents;
using JobCopilot.Domain.Applications;
using Microsoft.EntityFrameworkCore;

namespace JobCopilot.Infrastructure.Tests.Database;

[Collection(DatabaseCollection.Name)]
public class ApplicationServiceTests(PostgresFixture fixture)
{
    private static readonly DateOnly Today = new(2026, 11, 14);

    [Fact]
    public async Task SaveAd_CreatesOneCardHoweverOftenItIsCalled()
    {
        await using var app = await TestApp.CreateAsync(fixture);
        var ad = await app.SeedAdAsync("1", hidden: true);

        var first = await app.Applications.SaveAdAsync(ad.Id);
        var second = await app.Applications.SaveAdAsync(ad.Id);

        Assert.NotNull(first);
        Assert.Equal(first, second);

        var card = Assert.Single(await app.Applications.GetBoardAsync());
        Assert.Equal(ApplicationStatus.Saved, card.Status);
        Assert.Equal("Utvecklare", card.Headline);

        // Saving is a sign of interest, so the ad comes back out of hiding.
        Assert.False((await app.Ads.GetAsync(ad.Id))!.IsHidden);
    }

    [Fact]
    public async Task SaveAd_UnknownAd_ReturnsNull()
    {
        await using var app = await TestApp.CreateAsync(fixture);

        Assert.Null(await app.Applications.SaveAdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Move_ToSent_PersistsStatusAndInferredDates()
    {
        await using var app = await TestApp.CreateAsync(fixture);
        var id = (await app.Applications.SaveAdAsync((await app.SeedAdAsync("1")).Id))!.Value;
        app.Time.Advance(TimeSpan.FromHours(2));

        await app.Applications.MoveAsync(id, ApplicationStatus.Sent);

        var application = await app.Applications.GetAsync(id);
        Assert.NotNull(application);
        Assert.Equal(ApplicationStatus.Sent, application.Status);
        Assert.Equal(Today, application.AppliedOn);
        Assert.Equal(Today.AddDays(JobApplication.DefaultFollowUpDays), application.FollowUpOn);
        Assert.Equal(TestApp.Now.AddHours(2), application.StatusChangedAt);
    }

    [Fact]
    public async Task UpdateDetails_PersistsEverything()
    {
        await using var app = await TestApp.CreateAsync(fixture);
        var id = (await app.Applications.SaveAdAsync((await app.SeedAdAsync("1")).Id))!.Value;

        await app.Applications.UpdateDetailsAsync(id, new ApplicationDetails
        {
            AppliedOn = new DateOnly(2026, 11, 10),
            InterviewOn = new DateOnly(2026, 11, 20),
            FollowUpOn = new DateOnly(2026, 11, 17),
            Notes = "Ringde rekryteraren.",
            ContactName = "Karin Karlsson",
            ContactEmail = "karin@example.com",
            ContactPhone = "011-12 34 56",
        });

        var application = await app.Applications.GetAsync(id);
        Assert.NotNull(application);
        Assert.Equal(new DateOnly(2026, 11, 20), application.InterviewOn);
        Assert.Equal("Ringde rekryteraren.", application.Notes);
        Assert.Equal("Karin Karlsson", application.ContactName);

        var card = Assert.Single(await app.Applications.GetBoardAsync());
        Assert.Equal(new DateOnly(2026, 11, 10), card.AppliedOn);
        Assert.Equal("Karin Karlsson", card.ContactName);
    }

    [Fact]
    public async Task AddVersion_KeepsEveryVersionAndNumbersThem()
    {
        await using var app = await TestApp.CreateAsync(fixture);
        var id = (await app.Applications.SaveAdAsync((await app.SeedAdAsync("1")).Id))!.Value;

        var first = await app.Applications.AddVersionAsync(id, "CV ett", "Brev ett", null);
        app.Time.Advance(TimeSpan.FromMinutes(10));
        var second = await app.Applications.AddVersionAsync(id, "CV två", "Brev två", "kortare");

        Assert.Equal((1, 2), (first!.Number, second!.Number));

        var application = await app.Applications.GetAsync(id);
        Assert.NotNull(application);
        Assert.Equal([2, 1], application.Versions.Select(version => version.Number));
        Assert.Equal("CV två", application.LatestVersion!.Cv);
        Assert.Equal("kortare", application.LatestVersion.Label);

        // The first version is untouched by the second save.
        var original = application.Versions.Single(version => version.Number == 1);
        Assert.Equal(("CV ett", "Brev ett"), (original.Cv, original.CoverLetter));
        Assert.Equal(TestApp.Now, original.CreatedAt);

        Assert.Equal(2, Assert.Single(await app.Applications.GetBoardAsync()).VersionCount);
    }

    [Fact]
    public async Task AddVersion_UnknownApplication_ReturnsNull()
    {
        await using var app = await TestApp.CreateAsync(fixture);

        Assert.Null(await app.Applications.AddVersionAsync(Guid.NewGuid(), "cv", "brev", null));
    }

    [Fact]
    public async Task Delete_RemovesCardAndVersionsButKeepsTheAd()
    {
        await using var app = await TestApp.CreateAsync(fixture);
        var ad = await app.SeedAdAsync("1");
        var id = (await app.Applications.SaveAdAsync(ad.Id))!.Value;
        await app.Applications.AddVersionAsync(id, "cv", "brev", null);

        await app.Applications.DeleteAsync(id);

        Assert.Empty(await app.Applications.GetBoardAsync());
        Assert.NotNull(await app.Ads.GetAsync(ad.Id));

        await using var db = app.Database.CreateContext();
        Assert.Equal(0, await db.ApplicationVersions.CountAsync());
    }

    [Fact]
    public async Task GetDueFollowUps_ReturnsOnlyRemindersThatAreDue()
    {
        await using var app = await TestApp.CreateAsync(fixture);
        var due = (await app.Applications.SaveAdAsync((await app.SeedAdAsync("1", headline: "Försenad")).Id))!.Value;
        var later = (await app.Applications.SaveAdAsync((await app.SeedAdAsync("2", headline: "Senare")).Id))!.Value;
        await app.Applications.SaveAdAsync((await app.SeedAdAsync("3", headline: "Utan påminnelse")).Id);
        await app.Applications.UpdateDetailsAsync(due, new ApplicationDetails { FollowUpOn = Today });
        await app.Applications.UpdateDetailsAsync(later, new ApplicationDetails { FollowUpOn = Today.AddDays(1) });

        var followUps = await app.Applications.GetDueFollowUpsAsync();

        Assert.Equal("Försenad", Assert.Single(followUps).Headline);
    }

    [Fact]
    public async Task BuildPrompt_UsesStoredProfileAndAd()
    {
        await using var app = await TestApp.CreateAsync(fixture);
        await app.Profiles.SaveAsync(TestApp.Profile("C#"));
        var ad = await app.SeedAdAsync("1", headline: "Backendutvecklare", description: "Vi söker dig som kan C#.");
        var id = (await app.Applications.SaveAdAsync(ad.Id))!.Value;

        var prompt = await app.Applications.BuildPromptAsync(id);

        Assert.NotNull(prompt);
        Assert.Contains("Anna Andersson", prompt, StringComparison.Ordinal);
        Assert.Contains("Teknik: C# (god)", prompt, StringComparison.Ordinal);
        Assert.Contains("Rubrik: Backendutvecklare", prompt, StringComparison.Ordinal);
        Assert.Contains(ResponseMarkers.CoverLetter, prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("anna@example.com", prompt, StringComparison.Ordinal);
        Assert.Null(await app.Applications.BuildPromptAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Export_RendersSavedVersionAsPdf()
    {
        await using var app = await TestApp.CreateAsync(fixture);
        await app.Profiles.SaveAsync(TestApp.Profile("C#"));
        var id = (await app.Applications.SaveAdAsync((await app.SeedAdAsync("1")).Id))!.Value;
        var version = await app.Applications.AddVersionAsync(id, "## Profil\nUtvecklare.", "Hej!\n\nText.", null);

        var cv = await app.Export.ExportAsync(version!.Id, DocumentKind.Cv);
        var letter = await app.Export.ExportAsync(version.Id, DocumentKind.CoverLetter);

        Assert.NotNull(cv);
        Assert.NotNull(letter);
        Assert.Equal("CV - Anna Andersson - Bolaget AB.pdf", cv.FileName);
        Assert.Equal("Personligt brev - Anna Andersson - Bolaget AB.pdf", letter.FileName);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(cv.Content, 0, 5));
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(letter.Content, 0, 5));
        Assert.Null(await app.Export.ExportAsync(Guid.NewGuid(), DocumentKind.Cv));
    }
}
