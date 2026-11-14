using JobCopilot.Application.Common;

namespace JobCopilot.Web.Ui;

/// <summary>Formats dates and numbers the Swedish way, in Swedish time.</summary>
public static class Format
{
    public static string Date(DateTimeOffset? value) =>
        value is { } v ? SwedishTime.ToLocal(v).ToString("d MMM yyyy", SwedishTime.Culture) : "–";

    public static string Date(DateOnly? value) =>
        value is { } v ? v.ToString("d MMM yyyy", SwedishTime.Culture) : "–";

    public static string DateTime(DateTimeOffset? value) =>
        value is { } v ? SwedishTime.ToLocal(v).ToString("d MMM yyyy HH:mm", SwedishTime.Culture) : "–";

    public static string Percent(double share) => share.ToString("P0", SwedishTime.Culture);

    /// <summary>"idag", "imorgon", "om 5 dagar", "för 3 dagar sedan".</summary>
    public static string Relative(DateOnly date, DateOnly today)
    {
        var days = date.DayNumber - today.DayNumber;
        return days switch
        {
            0 => "idag",
            1 => "imorgon",
            -1 => "igår",
            > 1 => $"om {days} dagar",
            _ => $"för {-days} dagar sedan",
        };
    }

    /// <summary>Time left until an application deadline, or null when there is none.</summary>
    public static string? Deadline(DateTimeOffset? deadline, DateTimeOffset now)
    {
        if (deadline is not { } value)
        {
            return null;
        }

        var date = SwedishTime.ToLocalDate(value);
        var today = SwedishTime.ToLocalDate(now);
        var days = date.DayNumber - today.DayNumber;

        return days switch
        {
            < 0 => "Ansökningstiden har gått ut",
            0 => "Sista ansökningsdag idag",
            1 => "Sista ansökningsdag imorgon",
            <= 14 => $"{days} dagar kvar att söka",
            _ => $"Sök senast {Date(date)}",
        };
    }

    /// <summary>The CSS modifier for a match score: how good a fit the ad is.</summary>
    public static string ScoreTier(int score) => score switch
    {
        >= 70 => "high",
        >= 40 => "medium",
        _ => "low",
    };
}
