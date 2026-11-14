using JobCopilot.Domain.JobAds;

namespace JobCopilot.Domain.Tests.JobAds;

public class SavedSearchTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_NormalisesInput()
    {
        var search = SavedSearch.Create("  ", " .NET ", ["0581", " 0580 ", "0581", ""], remoteOnly: false, Now);

        Assert.Equal(".NET", search.Query);
        Assert.Equal(".NET", search.Name);
        Assert.Equal(["0581", "0580"], search.MunicipalityCodes);
        Assert.True(search.IsEnabled);
    }

    [Fact]
    public void Create_WithoutQueryOrMunicipality_Throws()
    {
        Assert.Throws<ArgumentException>(() => SavedSearch.Create("Tom", " ", [], remoteOnly: true, Now));
    }

    [Fact]
    public void Create_WithOnlyMunicipality_IsAllowed()
    {
        var search = SavedSearch.Create("Allt i Norrköping", "", ["0581"], remoteOnly: false, Now);

        Assert.Equal("", search.Query);
    }

    [Fact]
    public void RecordFailure_KeepsPreviousCountsAndTruncatesError()
    {
        var search = SavedSearch.Create("x", ".NET", [], false, Now);
        search.RecordSuccess(Now, resultCount: 12, newCount: 3);

        search.RecordFailure(Now.AddHours(1), new string('e', SavedSearch.MaxErrorLength + 50));

        Assert.False(search.LastRunSucceeded);
        Assert.Equal(SavedSearch.MaxErrorLength, search.LastError!.Length);
        Assert.Equal(12, search.LastResultCount);
        Assert.Equal(Now.AddHours(1), search.LastRunAt);
    }

    [Fact]
    public void RecordSuccess_ClearsPreviousError()
    {
        var search = SavedSearch.Create("x", ".NET", [], false, Now);
        search.RecordFailure(Now, "timeout");

        search.RecordSuccess(Now.AddHours(1), 5, 1);

        Assert.True(search.LastRunSucceeded);
        Assert.Null(search.LastError);
        Assert.Equal(1, search.LastNewCount);
    }
}
