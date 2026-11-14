using System.Globalization;

namespace JobCopilot.Application.Common;

/// <summary>
/// Everything is stored in UTC; the user, the job ads and "today" on the board live in Swedish time.
/// </summary>
public static class SwedishTime
{
    private static readonly TimeZoneInfo Zone = ResolveZone();

    public static CultureInfo Culture { get; } = CultureInfo.GetCultureInfo("sv-SE");

    /// <summary>Interprets a timestamp without offset (as sent by JobTech) as Swedish local time.</summary>
    public static DateTimeOffset ToUtc(DateTime swedishLocal)
    {
        var unspecified = DateTime.SpecifyKind(swedishLocal, DateTimeKind.Unspecified);

        // A time inside the spring-forward gap does not exist; nudge it past the gap instead of throwing.
        if (Zone.IsInvalidTime(unspecified))
        {
            unspecified = unspecified.AddHours(1);
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(unspecified, Zone), TimeSpan.Zero);
    }

    public static DateTimeOffset ToLocal(DateTimeOffset utc) => TimeZoneInfo.ConvertTime(utc, Zone);

    public static DateOnly Today(TimeProvider timeProvider) =>
        DateOnly.FromDateTime(ToLocal(timeProvider.GetUtcNow()).DateTime);

    public static DateOnly ToLocalDate(DateTimeOffset utc) => DateOnly.FromDateTime(ToLocal(utc).DateTime);

    private static TimeZoneInfo ResolveZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Minimal containers without tzdata: fall back to UTC rather than refusing to start.
            return TimeZoneInfo.Utc;
        }
    }
}
