using JobCopilot.Application.Abstractions;
using JobCopilot.Application.Common;
using JobCopilot.Application.Documents;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace JobCopilot.Infrastructure.Pdf;

/// <summary>
/// Renders the CV and cover letter with QuestPDF (Community licence). One restrained layout for
/// both documents: name and contact details on top, an accent colour for headings, plenty of air.
/// </summary>
internal sealed class QuestPdfRenderer : IPdfRenderer
{
    private static readonly Color Accent = Color.FromHex("#1F4E79");
    private static readonly Color Muted = Color.FromHex("#5F6B7A");
    private static readonly Color Rule = Color.FromHex("#C9D3DE");
    private static readonly Color Body = Color.FromHex("#1B1F24");

    private const float BodySize = 10f;

    static QuestPdfRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] RenderCv(CvDocument document) =>
        Document.Create(container => container.Page(page =>
        {
            ConfigurePage(page);
            page.Content().Column(column =>
            {
                column.Item().Element(c => ComposeHeader(c, document.Header));
                ComposeBlocks(column, document.Body);
            });
            page.Footer().AlignCenter().Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        }))
        .WithMetadata(Metadata("CV", document.Header))
        .GeneratePdf();

    public byte[] RenderCoverLetter(CoverLetterDocument document) =>
        Document.Create(container => container.Page(page =>
        {
            ConfigurePage(page);
            page.Content().Column(column =>
            {
                column.Item().Element(c => ComposeHeader(c, document.Header));

                column.Item().PaddingTop(22).Row(row =>
                {
                    row.RelativeItem().Text(document.Recipient).SemiBold();
                    row.AutoItem().Text(document.Date.ToString("d MMMM yyyy", SwedishTime.Culture)).FontColor(Muted);
                });
                column.Item().PaddingTop(14).PaddingBottom(4).Text(document.Subject).FontSize(12).Bold().FontColor(Accent);

                ComposeBlocks(column, document.Body, paragraphSpacing: 9, lineHeight: 1.45f);
            });
        }))
        .WithMetadata(Metadata("Personligt brev", document.Header))
        .GeneratePdf();

    private static void ConfigurePage(PageDescriptor page)
    {
        page.Size(PageSizes.A4);
        page.MarginVertical(1.8f, Unit.Centimetre);
        page.MarginHorizontal(2f, Unit.Centimetre);
        page.DefaultTextStyle(style => style.FontSize(BodySize).FontColor(Body).LineHeight(1.3f));
    }

    private static DocumentMetadata Metadata(string kind, DocumentHeader header) => new()
    {
        Title = string.IsNullOrWhiteSpace(header.FullName) ? kind : $"{kind} – {header.FullName}",
        Author = header.FullName,
        Creator = "JobCopilot",
    };

    private static void ComposeHeader(IContainer container, DocumentHeader header)
    {
        container.Column(column =>
        {
            if (!string.IsNullOrWhiteSpace(header.FullName))
            {
                column.Item().Text(header.FullName).FontSize(24).Bold().FontColor(Accent).LineHeight(1.1f);
            }

            if (!string.IsNullOrWhiteSpace(header.Headline))
            {
                column.Item().PaddingTop(2).Text(header.Headline).FontSize(12).FontColor(Muted);
            }

            foreach (var line in header.ContactLines)
            {
                column.Item().PaddingTop(3).Text(line).FontSize(9).FontColor(Muted);
            }

            column.Item().PaddingTop(10).LineHorizontal(1.5f).LineColor(Accent);
        });
    }

    private static void ComposeBlocks(
        ColumnDescriptor column, IReadOnlyList<MarkdownBlock> blocks, float paragraphSpacing = 5, float lineHeight = 1.3f)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case HeadingBlock { Level: <= 2 } heading:
                    column.Item().PaddingTop(14)
                        .DefaultTextStyle(style => style.FontSize(10.5f).Bold().FontColor(Accent).LetterSpacing(0.06f))
                        .Text(text => Runs(text, heading.Runs, upperCase: true));
                    column.Item().PaddingTop(2).PaddingBottom(2).LineHorizontal(0.75f).LineColor(Rule);
                    break;

                case HeadingBlock heading:
                    column.Item().PaddingTop(8).Element(c => ComposeEntryHeading(c, heading));
                    break;

                case BulletListBlock list:
                    foreach (var item in list.Items)
                    {
                        column.Item().PaddingTop(2).Row(row =>
                        {
                            row.ConstantItem(12).Text("•").FontColor(Accent);
                            row.RelativeItem()
                                .DefaultTextStyle(style => style.LineHeight(lineHeight))
                                .Text(text => Runs(text, item));
                        });
                    }

                    break;

                case ParagraphBlock paragraph:
                    column.Item().PaddingTop(paragraphSpacing)
                        .DefaultTextStyle(style => style.LineHeight(lineHeight))
                        .Text(text => Runs(text, paragraph.Runs));
                    break;
            }
        }
    }

    /// <summary>
    /// "### Title | Organisation | Period" becomes a bold title with the organisation after it and
    /// the period right-aligned – the conventional CV entry layout.
    /// </summary>
    private static void ComposeEntryHeading(IContainer container, HeadingBlock heading)
    {
        var parts = heading.PlainText.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var period = parts.Length > 1 && parts[^1].Any(char.IsDigit) ? parts[^1] : null;
        var leftParts = period is null ? parts : parts[..^1];

        container.Row(row =>
        {
            row.RelativeItem().Text(text =>
            {
                for (var i = 0; i < leftParts.Length; i++)
                {
                    if (i == 0)
                    {
                        text.Span(leftParts[i]).Bold().FontSize(10.5f);
                    }
                    else
                    {
                        text.Span("  ·  " + leftParts[i]).FontColor(Muted);
                    }
                }
            });

            if (period is not null)
            {
                row.AutoItem().PaddingLeft(10).Text(period).FontSize(9).FontColor(Muted);
            }
        });
    }

    private static void Runs(TextDescriptor text, IReadOnlyList<TextRun> runs, bool upperCase = false)
    {
        foreach (var run in runs)
        {
            var content = upperCase ? run.Text.ToUpper(SwedishTime.Culture) : run.Text;
            var span = text.Span(content);

            if (run.Bold)
            {
                span.Bold();
            }

            if (run.Italic)
            {
                span.Italic();
            }
        }
    }
}
