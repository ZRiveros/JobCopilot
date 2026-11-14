using JobCopilot.Application.Applications;

namespace JobCopilot.Application.Tests.Applications;

public class ApplicationResponseParserTests
{
    [Fact]
    public void Parse_WellFormedResponse_SplitsCvAndLetter()
    {
        var result = ApplicationResponseParser.Parse("""
            ===CV===
            ## Profil
            Utvecklare.
            ===BREV===
            Hej!

            Jag söker tjänsten.
            ===SLUT===
            """);

        Assert.True(result.IsSuccess);
        Assert.Equal("## Profil\nUtvecklare.", result.Cv);
        Assert.Equal("Hej!\n\nJag söker tjänsten.", result.CoverLetter);
    }

    [Fact]
    public void Parse_IgnoresTextBeforeFirstAndAfterLastMarker()
    {
        var result = ApplicationResponseParser.Parse("""
            Självklart! Här kommer ditt CV och brev:

            ===CV===
            CV-text
            ===BREV===
            Brevtext
            ===SLUT===

            Hoppas det hjälper! Vill du att jag justerar något?
            """);

        Assert.True(result.IsSuccess);
        Assert.Equal("CV-text", result.Cv);
        Assert.Equal("Brevtext", result.CoverLetter);
    }

    [Fact]
    public void Parse_WithoutEndMarker_TakesRestAsLetter()
    {
        var result = ApplicationResponseParser.Parse("===CV===\nCV-text\n===BREV===\nBrevtext\nMvh");

        Assert.True(result.IsSuccess);
        Assert.Equal("Brevtext\nMvh", result.CoverLetter);
    }

    [Theory]
    [InlineData("**===CV===**\nA\n**===BREV===**\nB\n**===SLUT===**")]
    [InlineData("=== CV ===\nA\n=== BREV ===\nB\n=== SLUT ===")]
    [InlineData("===cv===\nA\n===brev===\nB\n===slut===")]
    [InlineData("## ===CV===\nA\n## ===BREV===\nB")]
    [InlineData("`===CV===`\nA\n`===BREV===`\nB")]
    [InlineData("  ===CV===  \r\nA\r\n===BREV===\r\nB\r\n===SLUT===\r\n")]
    [InlineData("====CV====\nA\n====BREV====\nB")]
    public void Parse_ToleratesCommonMarkerVariations(string response)
    {
        var result = ApplicationResponseParser.Parse(response);

        Assert.True(result.IsSuccess);
        Assert.Equal("A", result.Cv);
        Assert.Equal("B", result.CoverLetter);
    }

    [Fact]
    public void Parse_RemovesCodeFencesAroundSections()
    {
        var result = ApplicationResponseParser.Parse("""
            ===CV===
            ```markdown
            ## Profil
            ```
            ===BREV===
            ```
            Hej!
            ```
            ===SLUT===
            """);

        Assert.True(result.IsSuccess);
        Assert.Equal("## Profil", result.Cv);
        Assert.Equal("Hej!", result.CoverLetter);
    }

    [Fact]
    public void Parse_WholeResponseInsideCodeFence_Works()
    {
        var result = ApplicationResponseParser.Parse("```\n===CV===\nA\n===BREV===\nB\n===SLUT===\n```");

        Assert.True(result.IsSuccess);
        Assert.Equal("A", result.Cv);
        Assert.Equal("B", result.CoverLetter);
    }

    [Fact]
    public void Parse_MarkerInsideSentence_IsNotAMarker()
    {
        var result = ApplicationResponseParser.Parse("Svara med ===CV=== och ===BREV=== tack.");

        Assert.Equal(ResponseParseError.MissingCvMarker, result.Error);
    }

    [Theory]
    [InlineData(null, ResponseParseError.Empty)]
    [InlineData("   ", ResponseParseError.Empty)]
    [InlineData("Bara text utan markörer", ResponseParseError.MissingCvMarker)]
    [InlineData("===BREV===\nB", ResponseParseError.MissingCvMarker)]
    [InlineData("===CV===\nA", ResponseParseError.MissingLetterMarker)]
    [InlineData("===BREV===\nB\n===CV===\nA", ResponseParseError.WrongOrder)]
    [InlineData("===CV===\n\n===BREV===\nB", ResponseParseError.EmptyCv)]
    [InlineData("===CV===\nA\n===BREV===\n\n===SLUT===", ResponseParseError.EmptyLetter)]
    public void Parse_InvalidResponse_ReportsWhatIsWrong(string? response, ResponseParseError expected)
    {
        var result = ApplicationResponseParser.Parse(response);

        Assert.False(result.IsSuccess);
        Assert.Equal(expected, result.Error);
        Assert.Equal("", result.Cv);
    }

    [Fact]
    public void Parse_UnderstandsTheFormatExampleInThePrompt()
    {
        // Guards the contract between prompt and parser: if the markers change in one place, this fails.
        var prompt = ApplicationPromptBuilder.Build(TestData.Profile(), TestData.Ad());

        var result = ApplicationResponseParser.Parse(prompt);

        Assert.True(result.IsSuccess);
        Assert.Equal("(CV:t)", result.Cv);
        Assert.Equal("(det personliga brevet)", result.CoverLetter);
    }
}
