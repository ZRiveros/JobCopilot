namespace JobCopilot.Application.Documents;

public enum DocumentKind
{
    Cv,
    CoverLetter,
}

/// <summary>
/// The identity block at the top of both documents. Always built from the master profile, never
/// from pasted text, so the contact details on an exported PDF are known to be correct.
/// </summary>
public sealed record DocumentHeader(string FullName, string Headline, IReadOnlyList<string> ContactLines);

public sealed record CvDocument(DocumentHeader Header, IReadOnlyList<MarkdownBlock> Body);

public sealed record CoverLetterDocument(
    DocumentHeader Header,
    string Subject,
    string Recipient,
    DateOnly Date,
    IReadOnlyList<MarkdownBlock> Body);

public sealed record ExportedDocument(string FileName, byte[] Content)
{
    public const string ContentType = "application/pdf";
}
