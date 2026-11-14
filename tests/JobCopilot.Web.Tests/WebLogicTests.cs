using JobCopilot.Application.Applications;
using JobCopilot.Application.JobAds;
using JobCopilot.Domain.Applications;
using JobCopilot.Web.Auth;
using JobCopilot.Web.Fetching;
using JobCopilot.Web.Ui;

namespace JobCopilot.Web.Tests;

public class AuthEndpointsTests
{
    [Fact]
    public void PasswordMatches_OnlyForTheExactPassword()
    {
        Assert.True(AuthEndpoints.PasswordMatches("rätt-lösenord", "rätt-lösenord"));
        Assert.False(AuthEndpoints.PasswordMatches("rätt-lösenorD", "rätt-lösenord"));
        Assert.False(AuthEndpoints.PasswordMatches("", "rätt-lösenord"));
    }

    [Fact]
    public void PasswordMatches_UnconfiguredPassword_NeverMatches()
    {
        Assert.False(AuthEndpoints.PasswordMatches("", ""));
    }

    [Theory]
    [InlineData("/", "/")]
    [InlineData("/ansokningar/123?tab=cv", "/ansokningar/123?tab=cv")]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    [InlineData("https://evil.example", "/")]
    [InlineData("//evil.example", "/")]
    [InlineData("/\\evil.example", "/")]
    [InlineData("ansokningar", "/")]
    [InlineData("/a\nb", "/")]
    public void ToLocalUrl_RejectsAnythingThatLeavesTheApp(string? returnUrl, string expected)
    {
        Assert.Equal(expected, AuthEndpoints.ToLocalUrl(returnUrl));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("kort", false)]
    [InlineData("        ", false)]
    [InlineData("tillräckligt-långt", true)]
    public void AuthOptions_RequiresARealPassword(string password, bool expected)
    {
        Assert.Equal(expected, new AuthOptions { Password = password }.HasUsablePassword);
    }
}

public class FormatTests
{
    private static readonly DateOnly Today = new(2026, 11, 14);
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, "idag")]
    [InlineData(1, "imorgon")]
    [InlineData(-1, "igår")]
    [InlineData(5, "om 5 dagar")]
    [InlineData(-3, "för 3 dagar sedan")]
    public void Relative_DescribesDistanceFromToday(int offsetDays, string expected)
    {
        Assert.Equal(expected, Format.Relative(Today.AddDays(offsetDays), Today));
    }

    [Theory]
    [InlineData(-1, "Ansökningstiden har gått ut")]
    [InlineData(0, "Sista ansökningsdag idag")]
    [InlineData(1, "Sista ansökningsdag imorgon")]
    [InlineData(10, "10 dagar kvar att söka")]
    public void Deadline_DescribesTimeLeft(int offsetDays, string expected)
    {
        Assert.Equal(expected, Format.Deadline(Now.AddDays(offsetDays), Now));
    }

    [Fact]
    public void Deadline_FarAway_ShowsTheDate()
    {
        Assert.Equal("Sök senast 14 dec. 2026", Format.Deadline(Now.AddDays(30), Now));
    }

    [Fact]
    public void Deadline_None_IsNull()
    {
        Assert.Null(Format.Deadline(null, Now));
    }

    [Fact]
    public void Date_UsesSwedishTime()
    {
        // 23:30 UTC is already the next day in Sweden.
        Assert.Equal("15 nov. 2026", Format.Date(new DateTimeOffset(2026, 11, 14, 23, 30, 0, TimeSpan.Zero)));
        Assert.Equal("–", Format.Date((DateTimeOffset?)null));
    }
}

public class LabelsTests
{
    [Fact]
    public void BoardColumns_CoverEveryStatus()
    {
        Assert.Equal(Enum.GetValues<ApplicationStatus>().Order(), Labels.BoardColumns.Order());
    }

    [Fact]
    public void EveryStatus_HasASwedishLabel()
    {
        Assert.All(Enum.GetValues<ApplicationStatus>(), status => Assert.NotEqual(status.ToString(), Labels.Of(status)));
    }

    [Fact]
    public void EveryParseError_HasAnExplanation()
    {
        var errors = Enum.GetValues<ResponseParseError>().Where(error => error != ResponseParseError.None);

        Assert.All(errors, error => Assert.NotEqual("Svaret kunde inte läsas in.", Labels.Of(error)));
    }

    [Fact]
    public void EveryViewAndSort_HasASwedishLabel()
    {
        Assert.All(Enum.GetValues<JobAdView>(), view => Assert.NotEqual(view.ToString(), Labels.Of(view)));
        Assert.All(Enum.GetValues<JobAdSort>(), sort => Assert.NotEqual(sort.ToString(), Labels.Of(sort)));
    }
}

public class FetchCoordinatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task WaitForNextRun_ReturnsImmediatelyWhenARunWasRequested()
    {
        using var coordinator = new FetchCoordinator();
        coordinator.RequestRun();

        var wait = coordinator.WaitForNextRunAsync(TimeSpan.FromMinutes(10), TestContext.Current.CancellationToken);

        await wait.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RequestRun_Repeated_CollapsesIntoOneRun()
    {
        using var coordinator = new FetchCoordinator();
        coordinator.RequestRun();
        coordinator.RequestRun();
        coordinator.RequestRun();

        await coordinator.WaitForNextRunAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var second = coordinator.WaitForNextRunAsync(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        await second;

        // The second wait ended by timeout, not by a queued request: nothing is left to consume.
        var third = coordinator.WaitForNextRunAsync(Timeout.InfiniteTimeSpan, TestContext.Current.CancellationToken);
        Assert.False(third.IsCompleted);
    }

    [Fact]
    public void StateChanges_AreVisibleAndRaiseChanged()
    {
        using var coordinator = new FetchCoordinator();
        var notifications = 0;
        coordinator.Changed += () => notifications++;

        coordinator.MarkStarted();
        Assert.True(coordinator.IsRunning);

        coordinator.MarkCompleted(new IngestionSummary(2, 0, 40, 5), Now);
        Assert.False(coordinator.IsRunning);
        Assert.False(coordinator.LastRunFailed);
        Assert.Equal(5, coordinator.LastSummary?.NewAds);
        Assert.Equal(Now, coordinator.LastCompletedAt);

        coordinator.MarkStarted();
        coordinator.MarkFailed(Now.AddHours(1));
        Assert.True(coordinator.LastRunFailed);
        Assert.Equal(4, notifications);
    }
}
