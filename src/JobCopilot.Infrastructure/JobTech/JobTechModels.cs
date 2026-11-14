namespace JobCopilot.Infrastructure.JobTech;

// Wire format of https://jobsearch.api.jobtechdev.se/search (snake_case JSON).
// Only the fields the app uses are mapped; everything is nullable because the API omits freely.

internal sealed record JobTechSearchResponse
{
    public JobTechTotal? Total { get; init; }
    public List<JobTechHit>? Hits { get; init; }
}

internal sealed record JobTechTotal
{
    public int Value { get; init; }
}

internal sealed record JobTechHit
{
    public string? Id { get; init; }
    public string? Headline { get; init; }
    public string? WebpageUrl { get; init; }
    public DateTime? ApplicationDeadline { get; init; }
    public DateTime? PublicationDate { get; init; }
    public bool? ExperienceRequired { get; init; }
    public bool? Removed { get; init; }
    public JobTechDescription? Description { get; init; }
    public JobTechLabel? EmploymentType { get; init; }
    public JobTechLabel? WorkingHoursType { get; init; }
    public JobTechLabel? WorkplaceModel { get; init; }
    public JobTechLabel? Occupation { get; init; }
    public JobTechEmployer? Employer { get; init; }
    public JobTechApplicationDetails? ApplicationDetails { get; init; }
    public JobTechAddress? WorkplaceAddress { get; init; }
    public JobTechRequirements? MustHave { get; init; }
    public JobTechRequirements? NiceToHave { get; init; }
}

internal sealed record JobTechDescription
{
    public string? Text { get; init; }
}

internal sealed record JobTechLabel
{
    public string? Label { get; init; }
}

internal sealed record JobTechEmployer
{
    public string? Name { get; init; }
    public string? Workplace { get; init; }
}

internal sealed record JobTechApplicationDetails
{
    public string? Email { get; init; }
    public string? Url { get; init; }
}

internal sealed record JobTechAddress
{
    public string? Municipality { get; init; }
    public string? Region { get; init; }
}

internal sealed record JobTechRequirements
{
    public List<JobTechLabel>? Skills { get; init; }
}
