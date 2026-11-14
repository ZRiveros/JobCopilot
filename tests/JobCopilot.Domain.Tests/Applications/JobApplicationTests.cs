using JobCopilot.Domain.Applications;

namespace JobCopilot.Domain.Tests.Applications;

public class JobApplicationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 14);

    private static JobApplication NewApplication() => JobApplication.CreateFor(Guid.NewGuid(), Now);

    [Fact]
    public void CreateFor_StartsInSavedColumn()
    {
        var application = NewApplication();

        Assert.Equal(ApplicationStatus.Saved, application.Status);
        Assert.Equal(Now, application.CreatedAt);
        Assert.Null(application.AppliedOn);
        Assert.Null(application.FollowUpOn);
    }

    [Fact]
    public void MoveTo_Sent_SetsAppliedDateAndSuggestsFollowUp()
    {
        var application = NewApplication();

        application.MoveTo(ApplicationStatus.Sent, Now.AddHours(1), Today);

        Assert.Equal(ApplicationStatus.Sent, application.Status);
        Assert.Equal(Today, application.AppliedOn);
        Assert.Equal(Today.AddDays(JobApplication.DefaultFollowUpDays), application.FollowUpOn);
        Assert.Equal(Now.AddHours(1), application.StatusChangedAt);
    }

    [Fact]
    public void MoveTo_Sent_KeepsDatesTheUserAlreadySet()
    {
        var application = NewApplication();
        var applied = new DateOnly(2026, 11, 1);
        var followUp = new DateOnly(2026, 11, 20);
        application.UpdateDetails(new ApplicationDetails { AppliedOn = applied, FollowUpOn = followUp });

        application.MoveTo(ApplicationStatus.Sent, Now, Today);

        Assert.Equal(applied, application.AppliedOn);
        Assert.Equal(followUp, application.FollowUpOn);
    }

    [Theory]
    [InlineData(ApplicationStatus.Offer)]
    [InlineData(ApplicationStatus.Rejected)]
    public void MoveTo_FinalOutcome_ClearsFollowUpReminder(ApplicationStatus outcome)
    {
        var application = NewApplication();
        application.MoveTo(ApplicationStatus.Sent, Now, Today);

        application.MoveTo(outcome, Now, Today);

        Assert.Null(application.FollowUpOn);
    }

    [Fact]
    public void MoveTo_SameStatus_DoesNothing()
    {
        var application = NewApplication();

        application.MoveTo(ApplicationStatus.Saved, Now.AddDays(1), Today);

        Assert.Equal(Now, application.StatusChangedAt);
    }

    [Fact]
    public void MoveTo_Backwards_IsAllowed()
    {
        var application = NewApplication();
        application.MoveTo(ApplicationStatus.Interview, Now, Today);

        application.MoveTo(ApplicationStatus.Saved, Now, Today);

        Assert.Equal(ApplicationStatus.Saved, application.Status);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void IsFollowUpDue_ComparesWithToday(int offsetDays, bool expected)
    {
        var application = NewApplication();
        application.UpdateDetails(new ApplicationDetails { FollowUpOn = Today.AddDays(offsetDays) });

        Assert.Equal(expected, application.IsFollowUpDue(Today));
    }

    [Fact]
    public void IsFollowUpDue_WithoutReminder_IsFalse()
    {
        Assert.False(NewApplication().IsFollowUpDue(Today));
    }

    [Fact]
    public void AddVersion_NumbersVersionsSequentially()
    {
        var application = NewApplication();

        var first = application.AddVersion("cv 1", "brev 1", null, Now);
        var second = application.AddVersion("cv 2", "brev 2", "  kortare  ", Now.AddMinutes(5));

        Assert.Equal(1, first.Number);
        Assert.Equal(2, second.Number);
        Assert.Equal("kortare", second.Label);
        Assert.Null(first.Label);
        Assert.Same(second, application.LatestVersion);
        Assert.Equal(2, application.Versions.Count);
    }

    [Fact]
    public void UpdateDetails_TrimsText()
    {
        var application = NewApplication();

        application.UpdateDetails(new ApplicationDetails
        {
            Notes = "  ringde  ",
            ContactName = " Anna ",
            ContactEmail = " anna@example.com ",
            ContactPhone = " 070 ",
        });

        Assert.Equal("ringde", application.Notes);
        Assert.Equal("Anna", application.ContactName);
        Assert.Equal("anna@example.com", application.ContactEmail);
        Assert.Equal("070", application.ContactPhone);
    }
}
