using JobCopilot.Domain.Matching;

namespace JobCopilot.Domain.Tests.Matching;

public class TextSearchTests
{
    [Theory]
    [InlineData("Vi söker en C#-utvecklare", "C#")]
    [InlineData("Erfarenhet av C#/.NET krävs", "C#")]
    [InlineData("Erfarenhet av C#/.NET krävs", ".NET")]
    [InlineData("Du kan .NET8 och HTML5", ".NET")]
    [InlineData("Du kan .NET8 och HTML5", "HTML")]
    [InlineData("Vi använder C++ och Python.", "C++")]
    [InlineData("java, kotlin", "Java")]
    [InlineData("(Docker)", "Docker")]
    [InlineData("SQL-databaser", "SQL")]
    public void Contains_TermAsWholeWord_ReturnsTrue(string text, string term)
    {
        Assert.True(TextSearch.Contains(text, term));
    }

    [Theory]
    [InlineData("Vi jobbar i JavaScript", "Java")]
    [InlineData("Vi jobbar i C# varje dag", "C")]
    [InlineData("Vi jobbar i C++ varje dag", "C")]
    [InlineData("Gitarrist sökes", "Git")]
    [InlineData("Scrummaster", "Scrum")]
    [InlineData("MySQL", "SQL")]
    public void Contains_TermInsideLongerWord_ReturnsFalse(string text, string term)
    {
        Assert.False(TextSearch.Contains(text, term));
    }

    [Fact]
    public void Contains_MatchCase_IgnoresOtherCasing()
    {
        Assert.False(TextSearch.Contains("the rest of the team", "REST", matchCase: true));
        Assert.True(TextSearch.Contains("REST and gRPC", "REST", matchCase: true));
    }

    [Fact]
    public void Contains_AllowSuffix_MatchesInflectedWord()
    {
        Assert.True(TextSearch.Contains("Vi söker seniora utvecklare", "senior", allowSuffix: true));
        Assert.False(TextSearch.Contains("Vi söker seniora utvecklare", "senior"));
    }

    [Fact]
    public void FindOccurrences_ReturnsEveryPosition()
    {
        var occurrences = TextSearch.FindOccurrences("Azure, azure och Azure DevOps", "Azure").ToList();

        Assert.Equal([0, 7, 17], occurrences.Select(o => o.Start));
    }

    [Theory]
    [InlineData("", "C#")]
    [InlineData("text", "")]
    public void FindOccurrences_EmptyInput_ReturnsNothing(string text, string term)
    {
        Assert.Empty(TextSearch.FindOccurrences(text, term));
    }
}
