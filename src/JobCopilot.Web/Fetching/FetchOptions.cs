using System.ComponentModel.DataAnnotations;

namespace JobCopilot.Web.Fetching;

public sealed class FetchOptions
{
    public const string SectionName = "Fetch";

    /// <summary>Turns the background fetch off, e.g. in tests. Manual fetches are then ignored too.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Minutes between fetches. JobTech is a shared public API, so be a polite client.</summary>
    [Range(5, 24 * 60)]
    public int IntervalMinutes { get; set; } = 60;

    /// <summary>Wait after start-up before the first fetch, so the app is responsive first.</summary>
    [Range(0, 3600)]
    public int StartupDelaySeconds { get; set; } = 20;
}
