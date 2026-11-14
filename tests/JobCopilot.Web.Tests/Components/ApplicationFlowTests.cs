using Bunit;
using JobCopilot.Application.Applications;
using JobCopilot.Web.Components.Shared;

namespace JobCopilot.Web.Tests.Components;

/// <summary>The copy-prompt / paste-answer flow that replaces an AI integration.</summary>
public class ApplicationFlowTests : BunitContext
{
    private const string CopyFunction = "jobCopilot.copyText";

    [Fact]
    public void PromptPanel_BeforeCreation_OffersOnlyToCreate()
    {
        var cut = Render<PromptPanel>();

        var button = Assert.Single(cut.FindAll("button"));
        Assert.Equal("Skapa prompt", button.TextContent.Trim());
        Assert.Empty(cut.FindAll("textarea"));
    }

    [Fact]
    public void PromptPanel_Create_RaisesOnCreate()
    {
        var created = 0;
        var cut = Render<PromptPanel>(parameters => parameters.Add(p => p.OnCreate, () => created++));

        cut.Find("button").Click();

        Assert.Equal(1, created);
    }

    [Fact]
    public void PromptPanel_WithPrompt_ShowsItForManualCopying()
    {
        var cut = Render<PromptPanel>(parameters => parameters.Add(p => p.Prompt, "PROMPTTEXT"));

        var textarea = cut.Find("textarea");
        Assert.Equal("PROMPTTEXT", textarea.GetAttribute("value"));
        Assert.True(textarea.HasAttribute("readonly"));
    }

    [Fact]
    public void PromptPanel_Copy_SendsPromptToClipboardAndConfirms()
    {
        var copy = JSInterop.Setup<bool>(CopyFunction, "PROMPTTEXT").SetResult(true);
        var cut = Render<PromptPanel>(parameters => parameters.Add(p => p.Prompt, "PROMPTTEXT"));

        ClickButton(cut, "Kopiera prompt");

        Assert.Single(copy.Invocations);
        Assert.Contains("Prompten är kopierad", cut.Find("[role=status]").TextContent);
    }

    [Fact]
    public void PromptPanel_CopyFails_TellsUserToCopyManually()
    {
        JSInterop.Setup<bool>(CopyFunction, "PROMPTTEXT").SetResult(false);
        var cut = Render<PromptPanel>(parameters => parameters.Add(p => p.Prompt, "PROMPTTEXT"));

        ClickButton(cut, "Kopiera prompt");

        Assert.Contains("Kunde inte kopiera automatiskt", cut.Find("[role=alert]").TextContent);
    }

    [Fact]
    public void PromptPanel_NewPrompt_ClearsCopiedConfirmation()
    {
        JSInterop.Setup<bool>(CopyFunction, _ => true).SetResult(true);
        var cut = Render<PromptPanel>(parameters => parameters.Add(p => p.Prompt, "FIRST"));
        ClickButton(cut, "Kopiera prompt");

        cut.Render(parameters => parameters.Add(p => p.Prompt, "SECOND"));

        Assert.Empty(cut.FindAll("[role=status]"));
    }

    [Fact]
    public void ResponseImport_ValidAnswer_RaisesSplitDocumentsAndClearsField()
    {
        ResponseParseResult? imported = null;
        var cut = Render<ResponseImport>(parameters => parameters.Add(p => p.OnImported, result => imported = result));

        cut.Find("textarea").Change("===CV===\n## Profil\n===BREV===\nHej!\n===SLUT===");
        ClickButton(cut, "Läs in svar");

        Assert.NotNull(imported);
        Assert.Equal("## Profil", imported.Cv);
        Assert.Equal("Hej!", imported.CoverLetter);
        Assert.Empty(cut.FindAll("[role=alert]"));
        Assert.Equal("", cut.Find("textarea").GetAttribute("value") ?? "");
    }

    [Theory]
    [InlineData("", "Klistra in svaret")]
    [InlineData("Här är ditt CV utan markörer", "===CV===")]
    [InlineData("===CV===\nBara ett CV", "===BREV===")]
    public void ResponseImport_InvalidAnswer_ExplainsWhatIsWrongAndDoesNotImport(string response, string expectedMessage)
    {
        var importedCount = 0;
        var cut = Render<ResponseImport>(parameters => parameters.Add(p => p.OnImported, _ => importedCount++));

        cut.Find("textarea").Change(response);
        ClickButton(cut, "Läs in svar");

        Assert.Contains(expectedMessage, cut.Find("[role=alert]").TextContent);
        Assert.Equal(0, importedCount);
    }

    [Fact]
    public void ResponseImport_WithUnsavedEdits_WarnsAboutOverwriting()
    {
        var cut = Render<ResponseImport>(parameters => parameters.Add(p => p.HasUnsavedEdits, true));

        Assert.Contains("Ersätter texten i editorn", cut.Markup);
    }

    [Fact]
    public void MarkdownView_RendersSupportedSubset()
    {
        var cut = Render<MarkdownView>(parameters => parameters.Add(p => p.Markdown,
            "## Erfarenhet\n### Utvecklare | Bolaget\n- Byggde **API:er**\n\nEtt stycke."));

        Assert.Equal("Erfarenhet", cut.Find("h2").TextContent);
        Assert.Equal("Utvecklare | Bolaget", cut.Find("h3").TextContent);
        Assert.Equal("API:er", cut.Find("li strong").TextContent);
        Assert.Equal("Ett stycke.", cut.Find("p").TextContent);
    }

    [Fact]
    public void MarkdownView_PastedHtml_IsShownAsTextNotMarkup()
    {
        var cut = Render<MarkdownView>(parameters => parameters.Add(p => p.Markdown, "<script>alert(1)</script>"));

        Assert.Empty(cut.FindAll("script"));
        Assert.Equal("<script>alert(1)</script>", cut.Find("p").TextContent);
    }

    [Fact]
    public void TagInput_SplitsTrimsAndDeduplicates()
    {
        List<string>? values = null;
        var cut = Render<TagInput>(parameters => parameters
            .Add(p => p.Values, ["C#"])
            .Add(p => p.ValuesChanged, changed => values = changed));

        Assert.Equal("C#", cut.Find("input").GetAttribute("value"));

        cut.Find("input").Change(" Docker, Azure ;docker,, ");

        Assert.Equal(["Docker", "Azure"], values);
    }

    private static void ClickButton<T>(IRenderedComponent<T> cut, string text)
        where T : Microsoft.AspNetCore.Components.IComponent =>
        cut.FindAll("button").Single(button => button.TextContent.Trim() == text).Click();
}
