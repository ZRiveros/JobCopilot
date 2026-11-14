namespace JobCopilot.Domain.JobAds;

/// <summary>
/// The source-provided content of an ad, independent of where it came from. Timestamps are UTC.
/// </summary>
public sealed record JobAdContent
{
    public required string ExternalId { get; init; }
    public required string Headline { get; init; }
    public string EmployerName { get; init; } = "";
    public string? Municipality { get; init; }
    public string? Region { get; init; }
    public string Description { get; init; } = "";
    public string? WebpageUrl { get; init; }
    public string? ApplicationUrl { get; init; }
    public string? ApplicationEmail { get; init; }
    public DateTimeOffset? PublishedAt { get; init; }
    public DateTimeOffset? ApplicationDeadline { get; init; }
    public string? EmploymentType { get; init; }
    public string? WorkingHours { get; init; }
    public string? WorkplaceModel { get; init; }
    public string? Occupation { get; init; }
    public bool ExperienceRequired { get; init; }
    public IReadOnlyList<string> DeclaredSkills { get; init; } = [];
}
