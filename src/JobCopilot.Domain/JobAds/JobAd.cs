using JobCopilot.Domain.Applications;
using JobCopilot.Domain.Matching;

namespace JobCopilot.Domain.JobAds;

/// <summary>
/// A job ad, either fetched from JobTech or added by hand, together with its match against the
/// master profile.
/// </summary>
public sealed class JobAd
{
    public const string JobTechSource = "JobTech";
    public const string ManualSource = "Manual";

    private JobAd()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Where the ad came from; unique together with <see cref="ExternalId"/>.</summary>
    public string Source { get; private set; } = JobTechSource;

    /// <summary>The ad id at the source. Used to de-duplicate between fetches and searches.</summary>
    public string ExternalId { get; private set; } = "";

    public string Headline { get; private set; } = "";
    public string EmployerName { get; private set; } = "";
    public string? Municipality { get; private set; }
    public string? Region { get; private set; }
    public string Description { get; private set; } = "";
    public string? WebpageUrl { get; private set; }
    public string? ApplicationUrl { get; private set; }
    public string? ApplicationEmail { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public DateTimeOffset? ApplicationDeadline { get; private set; }
    public string? EmploymentType { get; private set; }
    public string? WorkingHours { get; private set; }
    public string? WorkplaceModel { get; private set; }
    public string? Occupation { get; private set; }
    public bool ExperienceRequired { get; private set; }

    /// <summary>Structured skill requirements stated by the employer (often empty).</summary>
    public List<string> DeclaredSkills { get; private set; } = [];

    public DateTimeOffset FirstSeenAt { get; private set; }
    public DateTimeOffset LastSeenAt { get; private set; }

    public bool IsHidden { get; private set; }

    public int MatchScore { get; private set; }
    public List<string> MatchedSkills { get; private set; } = [];
    public List<string> MissingSkills { get; private set; } = [];
    public SeniorityHint Seniority { get; private set; }
    public DateTimeOffset? ScoredAt { get; private set; }

    /// <summary>Set when the ad has been saved to the application board.</summary>
    public JobApplication? Application { get; private set; }

    /// <summary>The best link for applying: the employer's own form if there is one, else the ad page.</summary>
    public string? ApplyUrl => ApplicationUrl ?? WebpageUrl;

    public static JobAd Create(string source, JobAdContent content, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(content.ExternalId);

        var ad = new JobAd
        {
            Id = Guid.CreateVersion7(now),
            Source = source,
            ExternalId = content.ExternalId,
            FirstSeenAt = now,
        };
        ad.Refresh(content, now);
        return ad;
    }

    /// <summary>Creates an ad the user found somewhere else and pasted in.</summary>
    public static JobAd CreateManual(
        string headline, string employerName, string? municipality, string description, string? url, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headline);

        var content = new JobAdContent
        {
            ExternalId = Guid.CreateVersion7(now).ToString("N"),
            Headline = headline.Trim(),
            EmployerName = employerName.Trim(),
            Municipality = string.IsNullOrWhiteSpace(municipality) ? null : municipality.Trim(),
            Description = description.Trim(),
            WebpageUrl = string.IsNullOrWhiteSpace(url) ? null : url.Trim(),
            PublishedAt = now,
        };

        return Create(ManualSource, content, now);
    }

    /// <summary>Updates the ad with the latest content from the source.</summary>
    public void Refresh(JobAdContent content, DateTimeOffset now)
    {
        Headline = content.Headline;
        EmployerName = content.EmployerName;
        Municipality = content.Municipality;
        Region = content.Region;
        Description = content.Description;
        WebpageUrl = content.WebpageUrl;
        ApplicationUrl = content.ApplicationUrl;
        ApplicationEmail = content.ApplicationEmail;
        PublishedAt = content.PublishedAt;
        ApplicationDeadline = content.ApplicationDeadline;
        EmploymentType = content.EmploymentType;
        WorkingHours = content.WorkingHours;
        WorkplaceModel = content.WorkplaceModel;
        Occupation = content.Occupation;
        ExperienceRequired = content.ExperienceRequired;
        DeclaredSkills = [.. content.DeclaredSkills];
        LastSeenAt = now;
    }

    public void ApplyMatch(MatchResult result, DateTimeOffset now)
    {
        MatchScore = result.Score;
        MatchedSkills = [.. result.MatchedSkills];
        MissingSkills = [.. result.MissingSkills];
        Seniority = result.Seniority;
        ScoredAt = now;
    }

    public void Hide() => IsHidden = true;

    public void Unhide() => IsHidden = false;

    public bool IsExpired(DateTimeOffset now) => ApplicationDeadline is { } deadline && deadline < now;
}
