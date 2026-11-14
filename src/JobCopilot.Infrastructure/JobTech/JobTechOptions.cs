using System.ComponentModel.DataAnnotations;

namespace JobCopilot.Infrastructure.JobTech;

public sealed class JobTechOptions
{
    public const string SectionName = "JobTech";

    /// <summary>JobSearch API limits: at most 100 ads per page and an offset of at most 2000.</summary>
    public const int MaxPageSize = 100;
    public const int MaxOffset = 2000;

    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://jobsearch.api.jobtechdev.se/";

    [Range(1, MaxPageSize)]
    public int PageSize { get; set; } = MaxPageSize;

    /// <summary>Upper bound per search and fetch, newest ads first. Keeps a broad query from flooding the feed.</summary>
    [Range(1, MaxOffset)]
    public int MaxAdsPerSearch { get; set; } = 300;

    /// <summary>Sent as User-Agent so the API owner can tell who is calling.</summary>
    [Required]
    public string UserAgent { get; set; } = "JobCopilot/1.0 (+https://github.com/ZRiveros/JobCopilot)";
}
