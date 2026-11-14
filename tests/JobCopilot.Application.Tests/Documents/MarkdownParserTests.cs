using JobCopilot.Application.Documents;

namespace JobCopilot.Application.Tests.Documents;

public class MarkdownParserTests
{
    [Fact]
    public void Parse_HeadingsBulletsAndParagraphs()
    {
        var blocks = MarkdownParser.Parse("""
            ## Erfarenhet
            ### Utvecklare | Exempel AB | 2024–2025
            - Byggde API:er
            - Skrev tester

            Ett stycke
            på två rader.
            """);

        Assert.Collection(
            blocks,
            block => Assert.Equal((2, "Erfarenhet"), Heading(block)),
            block => Assert.Equal((3, "Utvecklare | Exempel AB | 2024–2025"), Heading(block)),
            block =>
            {
                var list = Assert.IsType<BulletListBlock>(block);
                Assert.Equal(["Byggde API:er", "Skrev tester"], list.Items.Select(Plain));
            },
            block =>
            {
                var paragraph = Assert.IsType<ParagraphBlock>(block);
                Assert.Equal("Ett stycke\npå två rader.", Plain(paragraph.Runs));
            });
    }

    [Fact]
    public void Parse_BlankLineSeparatesParagraphs()
    {
        var blocks = MarkdownParser.Parse("Första.\n\nAndra.");

        Assert.Equal(2, blocks.Count);
        Assert.All(blocks, block => Assert.IsType<ParagraphBlock>(block));
    }

    [Theory]
    [InlineData("* punkt")]
    [InlineData("• punkt")]
    [InlineData("– punkt")]
    [InlineData("   - punkt")]
    public void Parse_AcceptsDifferentBulletMarkers(string line)
    {
        var list = Assert.IsType<BulletListBlock>(Assert.Single(MarkdownParser.Parse(line)));

        Assert.Equal("punkt", Plain(list.Items[0]));
    }

    [Fact]
    public void Parse_DeepHeadingsAreCappedAtLevelThree()
    {
        var heading = Assert.IsType<HeadingBlock>(Assert.Single(MarkdownParser.Parse("##### Liten")));

        Assert.Equal(3, heading.Level);
    }

    [Theory]
    [InlineData("#hashtag")]
    [InlineData("C# är kul")]
    [InlineData("#")]
    public void Parse_HashWithoutSpace_IsNotAHeading(string line)
    {
        Assert.IsType<ParagraphBlock>(Assert.Single(MarkdownParser.Parse(line)));
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsNoBlocks()
    {
        Assert.Empty(MarkdownParser.Parse(null));
        Assert.Empty(MarkdownParser.Parse("  \n \n"));
    }

    [Fact]
    public void ParseInline_BoldAndItalic()
    {
        var runs = MarkdownParser.ParseInline("Jag kan **C#** och *lite* Azure");

        Assert.Equal(
            [
                new TextRun("Jag kan "),
                new TextRun("C#", Bold: true),
                new TextRun(" och "),
                new TextRun("lite", Italic: true),
                new TextRun(" Azure"),
            ],
            runs);
    }

    [Theory]
    [InlineData("5 * 3 = 15")]
    [InlineData("En ensam ** markör")]
    [InlineData("Betyg: *")]
    public void ParseInline_UnpairedAsterisks_StayLiteral(string text)
    {
        var run = Assert.Single(MarkdownParser.ParseInline(text));

        Assert.Equal(new TextRun(text), run);
    }

    [Fact]
    public void ParseInline_BoldInsideHeading()
    {
        var heading = Assert.IsType<HeadingBlock>(Assert.Single(MarkdownParser.Parse("### **Utvecklare** | Bolaget")));

        Assert.True(heading.Runs[0].Bold);
        Assert.Equal("Utvecklare | Bolaget", heading.PlainText);
    }

    private static (int Level, string Text) Heading(MarkdownBlock block)
    {
        var heading = Assert.IsType<HeadingBlock>(block);
        return (heading.Level, heading.PlainText);
    }

    private static string Plain(IReadOnlyList<TextRun> runs) => string.Concat(runs.Select(run => run.Text));
}
