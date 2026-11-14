using System.Text;
using JobCopilot.Application.Documents;
using JobCopilot.Infrastructure.Pdf;

namespace JobCopilot.Infrastructure.Tests.Pdf;

public class QuestPdfRendererTests
{
    private static readonly DocumentHeader Header = new(
        "Anna Andersson",
        "Junior .NET-utvecklare",
        ["anna@example.com  ·  070-123 45 67  ·  Norrköping", "github.com/anna"]);

    private const string Cv = """
        ## Profil
        Nyfiken utvecklare med **C#** och *Blazor* som huvudspår. Åäö och – tankstreck ska fungera.

        ## Erfarenhet
        ### Systemutvecklare (LIA) | Exempel AB | jan. 2025 – juni 2025
        - Byggde ett internt verktyg i Blazor
        - Skrev enhetstester med xUnit

        ### Butikssäljare | Affären
        - Kundservice

        ## Utbildning
        ### .NET-utvecklare | Yrkeshögskolan | 2023 – 2025
        """;

    private readonly QuestPdfRenderer _renderer = new();

    [Fact]
    public void RenderCv_ProducesAPdf()
    {
        var pdf = _renderer.RenderCv(new CvDocument(Header, MarkdownParser.Parse(Cv)));

        AssertIsPdf(pdf);
    }

    [Fact]
    public void RenderCv_LongContent_FlowsOverSeveralPages()
    {
        var longCv = string.Join("\n\n", Enumerable.Range(1, 40).Select(i =>
            $"### Roll {i} | Bolag {i} | 20{i:00}\n- " + string.Join("\n- ", Enumerable.Repeat("En rad om vad jag gjorde i rollen.", 4))));

        var onePage = _renderer.RenderCv(new CvDocument(Header, MarkdownParser.Parse(Cv)));
        var manyPages = _renderer.RenderCv(new CvDocument(Header, MarkdownParser.Parse(longCv)));

        AssertIsPdf(manyPages);
        Assert.True(PageCount(manyPages) > PageCount(onePage));
    }

    [Fact]
    public void RenderCv_EmptyProfileAndBody_StillProducesAPdf()
    {
        var pdf = _renderer.RenderCv(new CvDocument(new DocumentHeader("", "", []), []));

        AssertIsPdf(pdf);
    }

    [Fact]
    public void RenderCoverLetter_ProducesAPdf()
    {
        var letter = new CoverLetterDocument(
            Header,
            "Ansökan: Backendutvecklare",
            "Bolaget AB",
            new DateOnly(2026, 11, 14),
            MarkdownParser.Parse("Hej!\n\nJag söker tjänsten eftersom den passar mig.\n\nMed vänliga hälsningar\nAnna Andersson"));

        var pdf = _renderer.RenderCoverLetter(letter);

        AssertIsPdf(pdf);
    }

    private static void AssertIsPdf(byte[] pdf)
    {
        Assert.True(pdf.Length > 1000, "The PDF is suspiciously small.");
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5));
    }

    /// <summary>Counts page objects; crude, but enough to tell one page from several.</summary>
    private static int PageCount(byte[] pdf)
    {
        var text = Encoding.Latin1.GetString(pdf);
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf("/Type /Page", index, StringComparison.Ordinal)) >= 0)
        {
            // "/Type /Pages" is the page tree node, not a page.
            if (index + 11 >= text.Length || text[index + 11] != 's')
            {
                count++;
            }

            index += 11;
        }

        return count;
    }
}
