using JobCopilot.Domain.JobAds;
using JobCopilot.Domain.Profiles;

namespace JobCopilot.Application.Documents;

/// <summary>
/// Combines edited text with facts from the profile and the ad into render-ready documents.
/// </summary>
public static class DocumentComposer
{
    public static DocumentHeader ComposeHeader(MasterProfile profile)
    {
        string[] contact = [profile.Email, profile.Phone, profile.Location];
        string[] links = [profile.LinkedInUrl, profile.GitHubUrl, profile.WebsiteUrl];

        var lines = new List<string>();
        AddLine(lines, contact);
        AddLine(lines, links.Select(ShortenUrl));

        return new DocumentHeader(profile.FullName, profile.Headline, lines);
    }

    public static CvDocument ComposeCv(MasterProfile profile, string cvMarkdown)
    {
        var blocks = MarkdownParser.Parse(cvMarkdown).ToList();

        // The header already shows the name; drop a leading "# Name" if the model added one anyway.
        if (blocks.Count > 0 && blocks[0] is HeadingBlock { Level: 1 })
        {
            blocks.RemoveAt(0);
        }

        return new CvDocument(ComposeHeader(profile), blocks);
    }

    public static CoverLetterDocument ComposeCoverLetter(MasterProfile profile, JobAd ad, string letterText, DateOnly date) =>
        new(
            ComposeHeader(profile),
            Subject: $"Ansökan: {ad.Headline}",
            Recipient: ad.EmployerName,
            date,
            MarkdownParser.Parse(letterText));

    /// <summary>A descriptive, file-system-safe name such as "CV - Anna Andersson - Exempel AB.pdf".</summary>
    public static string FileName(DocumentKind kind, MasterProfile profile, JobAd ad)
    {
        var prefix = kind == DocumentKind.Cv ? "CV" : "Personligt brev";
        string[] parts = [prefix, profile.FullName, ad.EmployerName];

        var name = string.Join(" - ", parts.Select(SanitizeFilePart).Where(part => part.Length > 0));
        return name + ".pdf";
    }

    private static void AddLine(List<string> lines, IEnumerable<string> parts)
    {
        var line = string.Join("  ·  ", parts.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part.Trim()));
        if (line.Length > 0)
        {
            lines.Add(line);
        }
    }

    internal static string ShortenUrl(string url)
    {
        var trimmed = url.Trim();
        foreach (var prefix in (string[])["https://", "http://", "www."])
        {
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[prefix.Length..];
            }
        }

        return trimmed.TrimEnd('/');
    }

    private static string SanitizeFilePart(string value)
    {
        // Path.GetInvalidFileNameChars differs per OS; the file is downloaded to any OS, so use the strictest set.
        const string Invalid = "<>:\"/\\|?*";
        var cleaned = new string(value.Where(c => !char.IsControl(c) && !Invalid.Contains(c)).ToArray()).Trim().TrimEnd('.');
        return cleaned.Length <= 60 ? cleaned : cleaned[..60].Trim();
    }
}
