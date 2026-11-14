using JobCopilot.Application.Documents;
using JobCopilot.Domain.Profiles;

namespace JobCopilot.Application.Tests.Documents;

public class DocumentComposerTests
{
    [Fact]
    public void ComposeHeader_TakesContactDetailsFromProfile()
    {
        var header = DocumentComposer.ComposeHeader(TestData.Profile());

        Assert.Equal("Anna Andersson", header.FullName);
        Assert.Equal("Junior .NET-utvecklare", header.Headline);
        Assert.Equal(
            ["anna@example.com  ·  070-123 45 67  ·  Norrköping", "linkedin.com/in/anna-andersson  ·  github.com/anna"],
            header.ContactLines);
    }

    [Fact]
    public void ComposeHeader_SkipsEmptyLines()
    {
        var header = DocumentComposer.ComposeHeader(new MasterProfile { FullName = "Anna", Email = "a@example.com" });

        Assert.Equal(["a@example.com"], header.ContactLines);
    }

    [Fact]
    public void ComposeCv_DropsLeadingNameHeading()
    {
        var cv = DocumentComposer.ComposeCv(TestData.Profile(), "# Anna Andersson\n## Profil\nText");

        var first = Assert.IsType<HeadingBlock>(cv.Body[0]);
        Assert.Equal("Profil", first.PlainText);
    }

    [Fact]
    public void ComposeCoverLetter_UsesAdForSubjectAndRecipient()
    {
        var date = new DateOnly(2026, 11, 14);

        var letter = DocumentComposer.ComposeCoverLetter(TestData.Profile(), TestData.Ad(), "Hej!\n\nText.", date);

        Assert.Equal("Ansökan: Backendutvecklare", letter.Subject);
        Assert.Equal("Bolaget AB", letter.Recipient);
        Assert.Equal(date, letter.Date);
        Assert.Equal(2, letter.Body.Count);
    }

    [Fact]
    public void FileName_DescribesDocumentAndEmployer()
    {
        var profile = TestData.Profile();
        var ad = TestData.Ad();

        Assert.Equal("CV - Anna Andersson - Bolaget AB.pdf", DocumentComposer.FileName(DocumentKind.Cv, profile, ad));
        Assert.Equal(
            "Personligt brev - Anna Andersson - Bolaget AB.pdf",
            DocumentComposer.FileName(DocumentKind.CoverLetter, profile, ad));
    }

    [Fact]
    public void FileName_RemovesCharactersThatAreInvalidInFileNames()
    {
        var profile = new MasterProfile { FullName = "A/B: \"C\"?" };

        var name = DocumentComposer.FileName(DocumentKind.Cv, profile, TestData.Ad());

        Assert.Equal("CV - AB C - Bolaget AB.pdf", name);
    }

    [Fact]
    public void FileName_EmptyProfileName_IsLeftOut()
    {
        Assert.Equal("CV - Bolaget AB.pdf", DocumentComposer.FileName(DocumentKind.Cv, new MasterProfile(), TestData.Ad()));
    }

    [Theory]
    [InlineData("https://www.linkedin.com/in/anna/", "linkedin.com/in/anna")]
    [InlineData("http://example.com", "example.com")]
    [InlineData("github.com/anna", "github.com/anna")]
    public void ShortenUrl_RemovesSchemeAndTrailingSlash(string url, string expected)
    {
        Assert.Equal(expected, DocumentComposer.ShortenUrl(url));
    }
}
