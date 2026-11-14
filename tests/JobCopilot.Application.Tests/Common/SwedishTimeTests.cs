using JobCopilot.Application.Common;

namespace JobCopilot.Application.Tests.Common;

public class SwedishTimeTests
{
    [Fact]
    public void ToUtc_WinterTime_IsOneHourAhead()
    {
        var utc = SwedishTime.ToUtc(new DateTime(2026, 11, 28, 23, 59, 59));

        Assert.Equal(new DateTimeOffset(2026, 11, 28, 22, 59, 59, TimeSpan.Zero), utc);
    }

    [Fact]
    public void ToUtc_SummerTime_IsTwoHoursAhead()
    {
        var utc = SwedishTime.ToUtc(new DateTime(2026, 6, 15, 12, 0, 0));

        Assert.Equal(new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero), utc);
    }

    [Fact]
    public void ToUtc_TimeInsideSpringForwardGap_DoesNotThrow()
    {
        var utc = SwedishTime.ToUtc(new DateTime(2026, 3, 29, 2, 30, 0));

        Assert.Equal(new DateTimeOffset(2026, 3, 29, 1, 30, 0, TimeSpan.Zero), utc);
    }

    [Fact]
    public void ToLocalDate_LateEveningUtc_IsNextDayInSweden()
    {
        var date = SwedishTime.ToLocalDate(new DateTimeOffset(2026, 11, 14, 23, 30, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 11, 15), date);
    }
}
