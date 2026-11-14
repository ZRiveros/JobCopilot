namespace JobCopilot.Application.Documents;

/// <summary>A piece of inline text with its emphasis.</summary>
public sealed record TextRun(string Text, bool Bold = false, bool Italic = false);

public abstract record MarkdownBlock;

/// <param name="Level">1–3.</param>
/// <param name="Runs">The heading text.</param>
public sealed record HeadingBlock(int Level, IReadOnlyList<TextRun> Runs) : MarkdownBlock
{
    public string PlainText => string.Concat(Runs.Select(run => run.Text));
}

/// <summary>A paragraph. Line breaks inside it are kept as <c>\n</c> in the runs.</summary>
public sealed record ParagraphBlock(IReadOnlyList<TextRun> Runs) : MarkdownBlock;

public sealed record BulletListBlock(IReadOnlyList<IReadOnlyList<TextRun>> Items) : MarkdownBlock;
