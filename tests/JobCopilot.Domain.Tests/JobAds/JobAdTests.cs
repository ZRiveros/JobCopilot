using JobCopilot.Domain.JobAds;
using JobCopilot.Domain.Matching;

namespace JobCopilot.Domain.Tests.JobAds;

public class JobAdTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 9, 0, 0, TimeSpan.Zero);

    private static JobAdContent Content(string id = "123") => new()
    {
        ExternalId = id,
        Headline = ".NET-utvecklare",
        EmployerName = "Exempel AB",
        Description = "C# och Azure.",
        WebpageUrl = "https://example.com/ad",
    };

    [Fact]
    public void Create_SetsIdentityAndTimestamps()
    {
        var ad = JobAd.Create(JobAd.JobTechSource, Content(), Now);

        Assert.NotEqual(Guid.Empty, ad.Id);
        Assert.Equal("123", ad.ExternalId);
        Assert.Equal(JobAd.JobTechSource, ad.Source);
        Assert.Equal(Now, ad.FirstSeenAt);
        Assert.Equal(Now, ad.LastSeenAt);
        Assert.False(ad.IsHidden);
    }

    [Fact]
    public void Refresh_UpdatesContentButKeepsFirstSeenAndUserState()
    {
        var ad = JobAd.Create(JobAd.JobTechSource, Content(), Now);
        ad.Hide();

        ad.Refresh(Content() with { Headline = "Backendutvecklare" }, Now.AddDays(1));

        Assert.Equal("Backendutvecklare", ad.Headline);
        Assert.Equal(Now, ad.FirstSeenAt);
        Assert.Equal(Now.AddDays(1), ad.LastSeenAt);
        Assert.True(ad.IsHidden);
    }

    [Fact]
    public void ApplyMatch_StoresResult()
    {
        var ad = JobAd.Create(JobAd.JobTechSource, Content(), Now);

        ad.ApplyMatch(new MatchResult(70, ["C#"], ["Azure"], SeniorityHint.Junior), Now);

        Assert.Equal(70, ad.MatchScore);
        Assert.Equal(["C#"], ad.MatchedSkills);
        Assert.Equal(["Azure"], ad.MissingSkills);
        Assert.Equal(SeniorityHint.Junior, ad.Seniority);
        Assert.Equal(Now, ad.ScoredAt);
    }

    [Fact]
    public void ApplyUrl_PrefersEmployersApplicationForm()
    {
        var withForm = JobAd.Create(JobAd.JobTechSource, Content() with { ApplicationUrl = "https://apply.example.com" }, Now);
        var withoutForm = JobAd.Create(JobAd.JobTechSource, Content("456"), Now);

        Assert.Equal("https://apply.example.com", withForm.ApplyUrl);
        Assert.Equal("https://example.com/ad", withoutForm.ApplyUrl);
    }

    [Fact]
    public void IsExpired_TrueOnlyAfterDeadline()
    {
        var ad = JobAd.Create(JobAd.JobTechSource, Content() with { ApplicationDeadline = Now.AddDays(1) }, Now);
        var noDeadline = JobAd.Create(JobAd.JobTechSource, Content("456"), Now);

        Assert.False(ad.IsExpired(Now));
        Assert.True(ad.IsExpired(Now.AddDays(2)));
        Assert.False(noDeadline.IsExpired(Now.AddYears(1)));
    }

    [Fact]
    public void CreateManual_GeneratesExternalIdAndTrims()
    {
        var ad = JobAd.CreateManual("  Utvecklare ", " Bolaget ", "  ", " Text ", " https://example.com ", Now);

        Assert.Equal(JobAd.ManualSource, ad.Source);
        Assert.False(string.IsNullOrWhiteSpace(ad.ExternalId));
        Assert.Equal("Utvecklare", ad.Headline);
        Assert.Equal("Bolaget", ad.EmployerName);
        Assert.Null(ad.Municipality);
        Assert.Equal("https://example.com", ad.WebpageUrl);
    }
}
