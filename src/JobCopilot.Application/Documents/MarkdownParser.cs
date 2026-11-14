using System.Text;

namespace JobCopilot.Application.Documents;

/// <summary>
/// Parses the small Markdown subset the prompt asks the model to use: headings (#, ##, ###),
/// bullet lists, paragraphs, **bold** and *italic*. Anything else is kept as plain text, which is
/// the safe choice for content that ends up in a PDF and in the preview.
/// </summary>
public static class MarkdownParser
{
    private static readonly string[] BulletPrefixes = ["- ", "* ", "• ", "– "];

    public static IReadOnlyList<MarkdownBlock> Parse(string? markdown)
    {
        var blocks = new List<MarkdownBlock>();
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return blocks;
        }

        var paragraph = new List<string>();
        var bullets = new List<IReadOnlyList<TextRun>>();

        void FlushParagraph()
        {
            if (paragraph.Count > 0)
            {
                blocks.Add(new ParagraphBlock(ParseInline(string.Join('\n', paragraph))));
                paragraph.Clear();
            }
        }

        void FlushBullets()
        {
            if (bullets.Count > 0)
            {
                blocks.Add(new BulletListBlock([.. bullets]));
                bullets.Clear();
            }
        }

        foreach (var rawLine in markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.Length == 0)
            {
                FlushParagraph();
                FlushBullets();
            }
            else if (TryParseHeading(line, out var heading))
            {
                FlushParagraph();
                FlushBullets();
                blocks.Add(heading);
            }
            else if (TryStripBullet(line, out var item))
            {
                FlushParagraph();
                bullets.Add(ParseInline(item));
            }
            else
            {
                FlushBullets();
                paragraph.Add(line);
            }
        }

        FlushParagraph();
        FlushBullets();
        return blocks;
    }

    /// <summary>Splits text into runs on <c>**bold**</c> and <c>*italic*</c>.</summary>
    public static IReadOnlyList<TextRun> ParseInline(string text)
    {
        var runs = new List<TextRun>();
        var buffer = new StringBuilder();
        var bold = false;
        var italic = false;

        void Flush()
        {
            if (buffer.Length > 0)
            {
                runs.Add(new TextRun(buffer.ToString(), bold, italic));
                buffer.Clear();
            }
        }

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '*')
            {
                buffer.Append(text[i]);
                continue;
            }

            var isDouble = i + 1 < text.Length && text[i + 1] == '*';
            var markerLength = isDouble ? 2 : 1;
            var isOpen = isDouble ? bold : italic;

            // An opening marker only counts if it is closed later; a lone asterisk stays literal.
            var closes = isOpen || HasClosingMarker(text, i + markerLength, isDouble);
            if (!closes)
            {
                buffer.Append('*', markerLength);
                i += markerLength - 1;
                continue;
            }

            Flush();
            if (isDouble)
            {
                bold = !bold;
            }
            else
            {
                italic = !italic;
            }

            i += markerLength - 1;
        }

        Flush();
        return runs;
    }

    private static bool HasClosingMarker(string text, int from, bool isDouble)
    {
        for (var i = from; i < text.Length; i++)
        {
            if (text[i] != '*')
            {
                continue;
            }

            var nextIsStar = i + 1 < text.Length && text[i + 1] == '*';
            if (isDouble && nextIsStar)
            {
                return i > from;
            }

            if (!isDouble && !nextIsStar)
            {
                return i > from;
            }

            if (nextIsStar)
            {
                i++;
            }
        }

        return false;
    }

    private static bool TryParseHeading(string line, out HeadingBlock heading)
    {
        heading = null!;
        var level = 0;
        while (level < line.Length && line[level] == '#')
        {
            level++;
        }

        if (level is 0 or > 6 || level >= line.Length || line[level] != ' ')
        {
            return false;
        }

        var text = line[level..].Trim().TrimEnd('#').Trim();
        if (text.Length == 0)
        {
            return false;
        }

        heading = new HeadingBlock(Math.Min(level, 3), ParseInline(text));
        return true;
    }

    private static bool TryStripBullet(string line, out string item)
    {
        foreach (var prefix in BulletPrefixes)
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
            {
                item = line[prefix.Length..].Trim();
                return item.Length > 0;
            }
        }

        item = "";
        return false;
    }
}
