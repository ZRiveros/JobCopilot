using Bunit;
using JobCopilot.Application.JobAds;
using JobCopilot.Domain.Applications;
using JobCopilot.Domain.JobAds;
using JobCopilot.Domain.Matching;
using JobCopilot.Web.Components.Shared;

namespace JobCopilot.Web.Tests.Components;

public class AdDisplayTests : BunitContext
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 9, 0, 0, TimeSpan.Zero);

    private static JobAdSummary Ad(Guid? applicationId = null, bool hidden = false) => new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Headline = "Junior .NET-utvecklare",
        EmployerName = "Exempel AB",
        Municipality = "Norrköping",
        Source = JobAd.JobTechSource,
        MatchScore = 72,
        MatchedSkills = ["C#", ".NET"],
        MissingSkills = ["Docker"],
        Seniority = SeniorityHint.Junior,
        ApplicationDeadline = Now.AddDays(5),
        IsHidden = hidden,
        ApplicationId = applicationId,
        ApplicationStatus = applicationId is null ? null : ApplicationStatus.Sent,
    };

    [Theory]
    [InlineData(85, "score-high")]
    [InlineData(70, "score-high")]
    [InlineData(55, "score-medium")]
    [InlineData(10, "score-low")]
    public void ScoreBadge_ColoursByTier(int score, string expectedClass)
    {
        var cut = Render<ScoreBadge>(parameters => parameters.Add(p => p.Score, score));

        var badge = cut.Find(".score");
        Assert.Contains(expectedClass, badge.ClassList);
        Assert.Contains(score.ToString(System.Globalization.CultureInfo.InvariantCulture), badge.TextContent);
    }

    [Fact]
    public void SkillChips_SeparatesMatchedFromMissing()
    {
        var cut = Render<SkillChips>(parameters => parameters
            .Add(p => p.Matched, ["C#", "SQL"])
            .Add(p => p.Missing, ["Docker"]));

        Assert.Equal(["C#", "SQL"], cut.FindAll(".chip-match").Select(chip => chip.TextContent));
        Assert.Equal(["Docker"], cut.FindAll(".chip-missing").Select(chip => chip.TextContent));
    }

    [Fact]
    public void SkillChips_WithMax_CutsLongListsAndShowsRemainder()
    {
        var cut = Render<SkillChips>(parameters => parameters
            .Add(p => p.Matched, ["A1", "A2", "A3", "A4", "A5"])
            .Add(p => p.Missing, [])
            .Add(p => p.Max, 3));

        Assert.Equal(3, cut.FindAll(".chip-match").Count);
        Assert.Equal("+2", cut.Find(".chip-more").TextContent);
    }

    [Fact]
    public void SkillChips_NoSkills_RendersNothing()
    {
        var cut = Render<SkillChips>(parameters => parameters.Add(p => p.Matched, []).Add(p => p.Missing, []));

        Assert.Empty(cut.FindAll("ul"));
    }

    [Fact]
    public void AdCard_ShowsAdWithLinkToDetailPage()
    {
        var cut = Render<AdCard>(parameters => parameters.Add(p => p.Ad, Ad()).Add(p => p.Now, Now));

        var link = cut.Find("h3 a");
        Assert.Equal("Junior .NET-utvecklare", link.TextContent);
        Assert.Equal("/annonser/11111111-1111-1111-1111-111111111111", link.GetAttribute("href"));
        Assert.Contains("Exempel AB", cut.Find(".ad-meta").TextContent);
        Assert.Contains("Norrköping", cut.Find(".ad-meta").TextContent);
        Assert.Contains("5 dagar kvar att söka", cut.Find(".ad-meta").TextContent);
        Assert.Contains("Junior-roll", cut.Markup);
    }

    [Fact]
    public void AdCard_Save_RaisesOnSaveWithTheAd()
    {
        JobAdSummary? saved = null;
        var ad = Ad();
        var cut = Render<AdCard>(parameters => parameters
            .Add(p => p.Ad, ad)
            .Add(p => p.Now, Now)
            .Add(p => p.OnSave, value => saved = value));

        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Spara").Click();

        Assert.Same(ad, saved);
    }

    [Fact]
    public void AdCard_AlreadySaved_LinksToTheApplicationInsteadOfSaving()
    {
        var applicationId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var cut = Render<AdCard>(parameters => parameters.Add(p => p.Ad, Ad(applicationId)).Add(p => p.Now, Now));

        Assert.DoesNotContain(cut.FindAll("button"), button => button.TextContent.Trim() == "Spara");
        Assert.Equal($"/ansokningar/{applicationId}", cut.Find(".ad-actions a").GetAttribute("href"));
        Assert.Contains("På tavlan: Skickad", cut.Markup);
    }

    [Theory]
    [InlineData(false, "Dölj")]
    [InlineData(true, "Visa igen")]
    public void AdCard_HideButton_ReflectsStateAndRaisesToggle(bool hidden, string expectedText)
    {
        JobAdSummary? toggled = null;
        var cut = Render<AdCard>(parameters => parameters
            .Add(p => p.Ad, Ad(hidden: hidden))
            .Add(p => p.Now, Now)
            .Add(p => p.OnToggleHidden, value => toggled = value));

        var button = cut.FindAll("button").Single(b => b.TextContent.Trim() == expectedText);
        button.Click();

        Assert.NotNull(toggled);
        Assert.Equal(hidden, cut.Find("article").ClassList.Contains("is-hidden"));
    }
}
