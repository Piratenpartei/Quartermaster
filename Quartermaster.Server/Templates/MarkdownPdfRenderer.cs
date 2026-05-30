using System.Linq;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Quartermaster.Server.Templates;

public static class MarkdownPdfRenderer {
    public static void RenderInto(ColumnDescriptor col, string markdown) {
        var document = Markdown.Parse(markdown);
        foreach (var block in document) {
            RenderBlock(col, block);
        }
    }

    private static void RenderBlock(ColumnDescriptor col, Block block) {
        switch (block) {
            case HeadingBlock heading:
                col.Item().PaddingTop(8).Text(t => {
                    var size = heading.Level switch {
                        1 => 18,
                        2 => 16,
                        3 => 14,
                        4 => 12,
                        _ => 11
                    };
                    t.DefaultTextStyle(x => x.FontSize(size).Bold());
                    if (heading.Inline != null)
                        RenderInlines(t, heading.Inline);
                });
                break;

            case ParagraphBlock paragraph:
                col.Item().Text(t => {
                    if (paragraph.Inline != null)
                        RenderInlines(t, paragraph.Inline);
                });
                break;

            case ListBlock list:
                var items = list.OfType<ListItemBlock>().ToList();
                for (var i = 0; i < items.Count; i++) {
                    var marker = list.IsOrdered ? $"{i + 1}." : "•";
                    col.Item().Row(row => {
                        row.ConstantItem(16).Text(marker);
                        row.RelativeItem().Column(inner => {
                            foreach (var child in items[i]) {
                                RenderBlock(inner, child);
                            }
                        });
                    });
                }
                break;

            case QuoteBlock quote:
                col.Item().BorderLeft(2).BorderColor(Colors.Grey.Medium).PaddingLeft(8).Column(inner => {
                    foreach (var child in quote) {
                        RenderBlock(inner, child);
                    }
                });
                break;

            case ThematicBreakBlock:
                col.Item().PaddingVertical(8).LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
                break;

            case FencedCodeBlock fenced:
                col.Item().Background(Colors.Grey.Lighten4).Padding(6)
                    .Text(string.Join('\n', fenced.Lines.Lines.Select(l => l.ToString())))
                    .FontFamily(Fonts.Consolas).FontSize(10);
                break;

            case CodeBlock code:
                col.Item().Background(Colors.Grey.Lighten4).Padding(6)
                    .Text(string.Join('\n', code.Lines.Lines.Select(l => l.ToString())))
                    .FontFamily(Fonts.Consolas).FontSize(10);
                break;
        }
    }

    private static void RenderInlines(TextDescriptor text, ContainerInline container, bool bold = false, bool italic = false) {
        foreach (var inline in container) {
            switch (inline) {
                case LiteralInline literal:
                    var span = text.Span(literal.Content.ToString());
                    if (bold)
                        span.Bold();
                    if (italic)
                        span.Italic();
                    break;

                case EmphasisInline emphasis:
                    var emBold = bold || emphasis.DelimiterCount >= 2;
                    var emItalic = italic || emphasis.DelimiterCount == 1 || emphasis.DelimiterCount == 3;
                    RenderInlines(text, emphasis, emBold, emItalic);
                    break;

                case CodeInline code:
                    text.Span(code.Content).FontFamily(Fonts.Consolas).BackgroundColor(Colors.Grey.Lighten4);
                    break;

                case LinkInline link:
                    var label = ExtractText(link);
                    text.Hyperlink(label, link.Url ?? "").FontColor(Colors.Blue.Medium).Underline();
                    break;

                case LineBreakInline:
                    text.Span("\n");
                    break;
            }
        }
    }

    private static string ExtractText(ContainerInline container) {
        var sb = new System.Text.StringBuilder();
        foreach (var inline in container) {
            if (inline is LiteralInline literal)
                sb.Append(literal.Content.ToString());
            else if (inline is ContainerInline nested)
                sb.Append(ExtractText(nested));
        }
        return sb.ToString();
    }
}
