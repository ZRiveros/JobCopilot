using JobCopilot.Domain.JobAds;

namespace JobCopilot.Domain.Applications;

/// <summary>
/// An ad the user has decided to act on. It is a card on the application board and owns every
/// saved version of the CV and cover letter written for that ad.
/// </summary>
public sealed class JobApplication
{
    /// <summary>Days after sending an application before a follow-up reminder is suggested.</summary>
    public const int DefaultFollowUpDays = 7;

    private readonly List<ApplicationVersion> _versions = [];

    private JobApplication()
    {
    }

    public Guid Id { get; private set; }
    public Guid JobAdId { get; private set; }
    public JobAd JobAd { get; private set; } = null!;

    public ApplicationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset StatusChangedAt { get; private set; }

    public DateOnly? AppliedOn { get; private set; }
    public DateOnly? InterviewOn { get; private set; }
    public DateOnly? FollowUpOn { get; private set; }

    public string Notes { get; private set; } = "";
    public string ContactName { get; private set; } = "";
    public string ContactEmail { get; private set; } = "";
    public string ContactPhone { get; private set; } = "";

    public IReadOnlyList<ApplicationVersion> Versions => _versions;

    public ApplicationVersion? LatestVersion => _versions.MaxBy(v => v.Number);

    public static JobApplication CreateFor(Guid jobAdId, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(now),
        JobAdId = jobAdId,
        Status = ApplicationStatus.Saved,
        CreatedAt = now,
        StatusChangedAt = now,
    };

    /// <summary>
    /// Moves the card to another column. Any move is allowed (the user may need to correct a
    /// mistake), but moving forward fills in what can be inferred:
    /// sending sets the applied date and suggests a follow-up, a final outcome clears the reminder.
    /// </summary>
    public void MoveTo(ApplicationStatus status, DateTimeOffset now, DateOnly today)
    {
        if (status == Status)
        {
            return;
        }

        Status = status;
        StatusChangedAt = now;

        switch (status)
        {
            case ApplicationStatus.Sent:
                AppliedOn ??= today;
                FollowUpOn ??= today.AddDays(DefaultFollowUpDays);
                break;
            case ApplicationStatus.Offer or ApplicationStatus.Rejected:
                FollowUpOn = null;
                break;
        }
    }

    public void UpdateDetails(ApplicationDetails details)
    {
        AppliedOn = details.AppliedOn;
        InterviewOn = details.InterviewOn;
        FollowUpOn = details.FollowUpOn;
        Notes = details.Notes.Trim();
        ContactName = details.ContactName.Trim();
        ContactEmail = details.ContactEmail.Trim();
        ContactPhone = details.ContactPhone.Trim();
    }

    public bool IsFollowUpDue(DateOnly today) => FollowUpOn is { } date && date <= today;

    /// <summary>Saves the current documents as a new, immutable version.</summary>
    public ApplicationVersion AddVersion(string cv, string coverLetter, string? label, DateTimeOffset now)
    {
        var number = _versions.Count == 0 ? 1 : _versions.Max(v => v.Number) + 1;
        var version = new ApplicationVersion(Id, number, cv, coverLetter, label, now);
        _versions.Add(version);
        return version;
    }
}

/// <summary>The editable details on an application card.</summary>
public sealed record ApplicationDetails
{
    public DateOnly? AppliedOn { get; init; }
    public DateOnly? InterviewOn { get; init; }
    public DateOnly? FollowUpOn { get; init; }
    public string Notes { get; init; } = "";
    public string ContactName { get; init; } = "";
    public string ContactEmail { get; init; } = "";
    public string ContactPhone { get; init; } = "";
}
