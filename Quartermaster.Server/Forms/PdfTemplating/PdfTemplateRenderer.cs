using System;
using System.Collections.Generic;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Quartermaster.Server.Forms.PdfTemplating.Nodes;

namespace Quartermaster.Server.Forms.PdfTemplating;

public delegate string? PdfTemplateAssetResolver(string path);

public static class PdfTemplateRenderer {
    public static byte[] Render(PageNode root, PdfTemplateAssetResolver assetResolver) {
        var document = Document.Create(container => {
            container.Page(page => {
                page.Margin((float)root.Margin);
                page.Size(ResolvePageSize(root.Size));
                page.DefaultTextStyle(x => x.FontSize((float)root.FontSize).FontFamily(root.FontFamily));
                page.Content().Element(c => RenderChildren(c, root.Children, assetResolver));
            });
        });
        return document.GeneratePdf();
    }

    private static PageSize ResolvePageSize(string size) => size.ToUpperInvariant() switch {
        "A4" => PageSizes.A4,
        "A5" => PageSizes.A5,
        "LETTER" => PageSizes.Letter,
        _ => PageSizes.A4
    };

    private static void RenderChildren(IContainer container, List<PdfNode> children, PdfTemplateAssetResolver assets) {
        if (children.Count == 0) {
            container.Text("");
            return;
        }
        if (children.Count == 1) {
            RenderNode(container, children[0], assets);
            return;
        }
        container.Column(col => {
            foreach (var child in children) {
                col.Item().Element(c => RenderNode(c, child, assets));
            }
        });
    }

    private static void RenderNode(IContainer container, PdfNode node, PdfTemplateAssetResolver assets) {
        switch (node) {
            case ColumnNode col: RenderColumn(container, col, assets); break;
            case RowNode row: RenderRow(container, row, assets); break;
            case DivNode div: RenderChildren(ApplyDivStyle(container, div), div.Children, assets); break;
            case TextNode text: RenderText(container, text); break;
            case SvgNode svg: RenderSvg(container, svg, assets); break;
            case ImageNode img: RenderImage(container, img, assets); break;
            case HrNode hr: RenderHr(container, hr); break;
            case SpacerNode spacer: RenderSpacer(container, spacer); break;
            case InputNode input: RenderInput(container, input); break;
            case TableNode table: RenderTable(container, table, assets); break;
            default:
                throw new InvalidOperationException($"Unsupported node {node.GetType().Name}");
        }
    }

    private static void RenderColumn(IContainer container, ColumnNode col, PdfTemplateAssetResolver assets) {
        IContainer c = container;
        if (col.PadTop > 0) c = c.PaddingTop((float)col.PadTop);
        if (col.PadBottom > 0) c = c.PaddingBottom((float)col.PadBottom);
        if (col.PadLeft > 0) c = c.PaddingLeft((float)col.PadLeft);
        if (col.PadRight > 0) c = c.PaddingRight((float)col.PadRight);
        c.Column(qc => {
            if (col.Spacing > 0) qc.Spacing((float)col.Spacing);
            foreach (var child in col.Children) {
                qc.Item().Element(cc => RenderNode(cc, child, assets));
            }
        });
    }

    private static void RenderRow(IContainer container, RowNode row, PdfTemplateAssetResolver assets) {
        var c = row.PadTop > 0 ? container.PaddingTop((float)row.PadTop) : container;
        c.Row(qr => {
            if (row.Spacing > 0) qr.Spacing((float)row.Spacing);
            foreach (var node in row.Children) {
                if (node is DivNode div) {
                    var cell = div.Width.HasValue
                        ? qr.ConstantItem((float)div.Width.Value)
                        : qr.RelativeItem((float)div.Weight);
                    RenderChildren(ApplyDivStyle(cell, div), div.Children, assets);
                } else {
                    qr.RelativeItem().Element(cc => RenderNode(cc, node, assets));
                }
            }
        });
    }

    private static IContainer ApplyDivStyle(IContainer container, DivNode div) {
        IContainer c = container;
        if (div.PadTop > 0) c = c.PaddingTop((float)div.PadTop);
        if (div.PadBottom > 0) c = c.PaddingBottom((float)div.PadBottom);
        if (div.PadLeft > 0) c = c.PaddingLeft((float)div.PadLeft);
        if (div.PadRight > 0) c = c.PaddingRight((float)div.PadRight);
        if (div.Padding > 0) c = c.Padding((float)div.Padding);
        if (div.Border > 0) c = c.Border((float)div.Border);
        if (div.BorderTop > 0) c = c.BorderTop((float)div.BorderTop);
        if (div.BorderBottom > 0) c = c.BorderBottom((float)div.BorderBottom);
        if (div.BorderLeft > 0) c = c.BorderLeft((float)div.BorderLeft);
        if (div.BorderRight > 0) c = c.BorderRight((float)div.BorderRight);
        if (div.BorderColor != null) c = c.BorderColor(ResolveColor(div.BorderColor));
        if (div.Background != null) c = c.Background(ResolveColor(div.Background));
        if (div.MinHeight > 0) c = c.MinHeight((float)div.MinHeight);
        if (div.VerticalAlign == "middle") c = c.AlignMiddle();
        else if (div.VerticalAlign == "bottom") c = c.AlignBottom();
        if (div.Align == "right") c = c.AlignRight();
        else if (div.Align == "center") c = c.AlignCenter();
        return c;
    }

    private static void RenderText(IContainer container, TextNode text) {
        var c = container;
        if (text.PadTop > 0) c = c.PaddingTop((float)text.PadTop);
        if (text.Align == "right") c = c.AlignRight();
        else if (text.Align == "center") c = c.AlignCenter();

        c.Text(t => {
            if (text.Size.HasValue) {
                t.DefaultTextStyle(x => x.FontSize((float)text.Size.Value));
            }
            if (text.Color != null) {
                t.DefaultTextStyle(x => x.FontColor(ResolveColor(text.Color)));
            }
            foreach (var span in text.Spans) {
                var s = t.Span(span.Text);
                if (span.Size.HasValue) s = s.FontSize((float)span.Size.Value);
                if (span.Bold || text.Bold) s = s.Bold();
                if (span.Italic || text.Italic) s = s.Italic();
                if (span.Underline) s = s.Underline();
                if (span.Color != null) s = s.FontColor(ResolveColor(span.Color));
                else if (text.Color != null) s = s.FontColor(ResolveColor(text.Color));
            }
        });
    }

    private static void RenderSvg(IContainer container, SvgNode svg, PdfTemplateAssetResolver assets) {
        var content = assets(svg.Path);
        if (content == null) {
            container.Text("");
            return;
        }
        IContainer c = container;
        if (svg.Width.HasValue) c = c.Width((float)svg.Width.Value);
        if (svg.Height.HasValue) c = c.Height((float)svg.Height.Value);
        c.Svg(content);
    }

    private static void RenderImage(IContainer container, ImageNode img, PdfTemplateAssetResolver assets) {
        var content = assets(img.Path);
        if (content == null) {
            container.Text("");
            return;
        }
        IContainer c = container;
        if (img.Width.HasValue) c = c.Width((float)img.Width.Value);
        if (img.Height.HasValue) c = c.Height((float)img.Height.Value);
        c.Image(Convert.FromBase64String(content));
    }

    private static void RenderHr(IContainer container, HrNode hr) {
        var c = container;
        if (hr.PadTop > 0) c = c.PaddingTop((float)hr.PadTop);
        if (hr.PadBottom > 0) c = c.PaddingBottom((float)hr.PadBottom);
        var line = c.LineHorizontal((float)hr.Thickness);
        if (hr.Color != null) line.LineColor(ResolveColor(hr.Color));
    }

    private static void RenderSpacer(IContainer container, SpacerNode spacer) {
        IContainer c = container;
        if (spacer.Height > 0) c = c.Height((float)spacer.Height);
        if (spacer.Width > 0) c = c.Width((float)spacer.Width);
        c.Container();
    }

    private static void RenderInput(IContainer container, InputNode input) {
        // Only the checkbox type renders today: a small bordered square with an "X" when checked.
        container.Width((float)input.Size + 5).AlignTop().Border(0.5f).Height((float)input.Size).AlignCenter().AlignMiddle()
            .Text(input.Checked ? "X" : " ").FontSize(9).Bold();
    }

    private static void RenderTable(IContainer container, TableNode table, PdfTemplateAssetResolver assets) {
        container.Table(qt => {
            qt.ColumnsDefinition(cd => {
                foreach (var col in table.Columns) {
                    if (col.Width.HasValue) cd.ConstantColumn((float)col.Width.Value);
                    else cd.RelativeColumn((float)col.Weight);
                }
            });

            if (table.Header != null) {
                qt.Header(h => {
                    foreach (var cell in table.Header.Cells) {
                        var qc = h.Cell();
                        if (cell.ColSpan > 1) qc = qc.ColumnSpan((uint)cell.ColSpan);
                        qc.Element(c => ApplyCellStyle(c, cell)).Element(c => RenderChildren(c, cell.Children, assets));
                    }
                });
            }

            foreach (var row in table.Rows) {
                foreach (var cell in row.Cells) {
                    var qc = qt.Cell();
                    if (cell.ColSpan > 1) qc = qc.ColumnSpan((uint)cell.ColSpan);
                    qc.Element(c => ApplyCellStyle(c, cell)).Element(c => RenderChildren(c, cell.Children, assets));
                }
            }

            if (table.Footer != null) {
                qt.Footer(f => {
                    foreach (var cell in table.Footer.Cells) {
                        var qc = f.Cell();
                        if (cell.ColSpan > 1) qc = qc.ColumnSpan((uint)cell.ColSpan);
                        qc.Element(c => ApplyCellStyle(c, cell)).Element(c => RenderChildren(c, cell.Children, assets));
                    }
                });
            }
        });
    }

    private static IContainer ApplyCellStyle(IContainer c, TableCellNode cell) {
        if (cell.Background != null) c = c.Background(ResolveColor(cell.Background));
        if (cell.BorderWidth > 0) c = c.Border((float)cell.BorderWidth);
        if (cell.BorderColor != null) c = c.BorderColor(ResolveColor(cell.BorderColor));
        if (cell.PadVertical > 0) c = c.PaddingVertical((float)cell.PadVertical);
        if (cell.PadHorizontal > 0) c = c.PaddingHorizontal((float)cell.PadHorizontal);
        if (cell.MinHeight > 0) c = c.MinHeight((float)cell.MinHeight);
        if (cell.VerticalAlign == "middle") c = c.AlignMiddle();
        else if (cell.VerticalAlign == "bottom") c = c.AlignBottom();
        if (cell.Align == "right") c = c.AlignRight();
        else if (cell.Align == "center") c = c.AlignCenter();
        return c;
    }

    private static string ResolveColor(string token) {
        if (token.StartsWith("#")) return token;
        return token switch {
            "black" => Colors.Black,
            "white" => Colors.White,
            "grey-lighten-3" => Colors.Grey.Lighten3,
            "grey-lighten-2" => Colors.Grey.Lighten2,
            "grey-lighten-1" => Colors.Grey.Lighten1,
            "grey-medium" => Colors.Grey.Medium,
            "grey-darken-1" => Colors.Grey.Darken1,
            "grey-darken-2" => Colors.Grey.Darken2,
            "grey-darken-3" => Colors.Grey.Darken3,
            _ => Colors.Black
        };
    }
}
