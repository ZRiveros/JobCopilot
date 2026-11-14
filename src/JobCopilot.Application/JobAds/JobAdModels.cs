using JobCopilot.Domain.Applications;
using JobCopilot.Domain.Matching;

namespace JobCopilot.Application.JobAds;

public enum JobAdView
{
    /// <summary>Everything that is not hidden.</summary>
    Active = 0,

    /// <summary>Ads that have a card on the application board.</summary>
    Saved = 1,
    Hidden = 2,
    All = 3,
}

public enum JobAdSort
{
    BestMatch = 0,
    Newest = 1,
    Deadline = 2,
}

public sealed record JobAdFilter
{
    public string? Search { get; init; }
    public int MinScore { get; init; }
    public JobAdView View { get; init; } = JobAdView.Active;
    public string? Municipality { get; init; }

    /// <summary>Only ads that mention this skill, whether the profile has it or not.</summary>
    public string? Skill { get; init; }

    public bool IncludeExpired { get; init; }
    public JobAdSort Sort { get; init; } = JobAdSort.BestMatch;
    public int Skip { get; init; }
    public int Take { get; init; } = 30;
}

/// <summary>A row in the ranked ad list. Leaves out the description, which can be large.</summary>
public sealed record JobAdSummary
{
    public required Guid Id { get; init; }
    public required string Headline { get; init; }
    public required string EmployerName { get; init; }
    public string? Municipality { get; init; }
    public string? WorkplaceModel { get; init; }
    public DateTimeOffset? PublishedAt { get; init; }
    public DateTimeOffset? ApplicationDeadline { get; init; }
    public DateTimeOffset FirstSeenAt { get; init; }
    public required string Source { get; init; }
    public int MatchScore { get; init; }
    public IReadOnlyList<string> MatchedSkills { get; init; } = [];
    public IReadOnlyList<string> MissingSkills { get; init; } = [];
    public SeniorityHint Seniority { get; init; }
    public bool IsHidden { get; init; }
    public Guid? ApplicationId { get; init; }
    public ApplicationStatus? ApplicationStatus { get; init; }

    public bool IsSaved => ApplicationId is not null;
}

public sealed record ManualJobAdInput
{
    public string Headline { get; init; } = "";
    public string EmployerName { get; init; } = "";
    public string Municipality { get; init; } = "";
    public string Url { get; init; } = "";
    public string Description { get; init; } = "";
}
