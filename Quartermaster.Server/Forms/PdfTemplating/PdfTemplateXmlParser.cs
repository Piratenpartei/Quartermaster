using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Quartermaster.Server.Forms.PdfTemplating.Nodes;

namespace Quartermaster.Server.Forms.PdfTemplating;

public class PdfTemplateParseException : Exception {
    public PdfTemplateParseException(string message) : base(message) { }
}

/// <summary>Turns the HTML-flavoured XML produced by <see cref="PdfTemplateEngine"/> into a tree of <see cref="PdfNode"/>s. Pure data, no QuestPDF dependency.</summary>
public static class PdfTemplateXmlParser {
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static PageNode Parse(string xml) {
        XDocument doc;
        try {
            doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        } catch (Exception ex) {
            throw new PdfTemplateParseException("Template did not produce valid XML after Fluid rendering: " + ex.Message);
        }
        var root = doc.Root ?? throw new PdfTemplateParseException("Template produced empty document.");
        if (root.Name.LocalName != "page") {
            throw new PdfTemplateParseException($"Root element must be <page>, got <{root.Name.LocalName}>.");
        }
        return ParsePage(root);
    }

    private static PageNode ParsePage(XElement e) {
        var page = new PageNode {
            Size = StringAttr(e, "size", "A4"),
            Margin = DoubleAttr(e, "margin", 36),
            FontFamily = StringAttr(e, "font-family", "Helvetica"),
            FontSize = DoubleAttr(e, "font-size", 10)
        };
        ParseChildren(e, page.Children);
        return page;
    }

    private static void ParseChildren(XElement parent, System.Collections.Generic.List<PdfNode> target) {
        foreach (var node in parent.Nodes()) {
            switch (node) {
                case XElement el:
                    target.Add(ParseElement(el));
                    break;
                case XText xt when !string.IsNullOrWhiteSpace(xt.Value):
                    target.Add(WrapText(xt.Value));
                    break;
            }
        }
    }

    private static PdfNode ParseElement(XElement e) {
        return e.Name.LocalName switch {
            "column" => ParseColumn(e),
            "row" => ParseRow(e),
            "div" => ParseDiv(e),
            "p" => ParseText(e),
            "svg" => ParseSvg(e),
            "image" => ParseImage(e),
            "hr" => ParseHr(e),
            "spacer" => ParseSpacer(e),
            "input" => ParseInput(e),
            "table" => ParseTable(e),
            _ => throw new PdfTemplateParseException($"Unknown element <{e.Name.LocalName}>.")
        };
    }

    private static ColumnNode ParseColumn(XElement e) {
        var col = new ColumnNode {
            Spacing = DoubleAttr(e, "spacing", 0),
            PadTop = DoubleAttr(e, "pad-top", 0),
            PadBottom = DoubleAttr(e, "pad-bottom", 0),
            PadLeft = DoubleAttr(e, "pad-left", 0),
            PadRight = DoubleAttr(e, "pad-right", 0)
        };
        ParseChildren(e, col.Children);
        return col;
    }

    private static RowNode ParseRow(XElement e) {
        var row = new RowNode {
            Spacing = DoubleAttr(e, "spacing", 0),
            PadTop = DoubleAttr(e, "pad-top", 0)
        };
        ParseChildren(e, row.Children);
        return row;
    }

    private static DivNode ParseDiv(XElement e) {
        var div = new DivNode {
            Width = NullableDoubleAttr(e, "width"),
            Weight = DoubleAttr(e, "weight", 1),
            Align = e.Attribute("align")?.Value,
            VerticalAlign = e.Attribute("valign")?.Value,
            Background = e.Attribute("background")?.Value,
            BorderColor = e.Attribute("border-color")?.Value,
            Border = DoubleAttr(e, "border", 0),
            BorderTop = DoubleAttr(e, "border-top", 0),
            BorderBottom = DoubleAttr(e, "border-bottom", 0),
            BorderLeft = DoubleAttr(e, "border-left", 0),
            BorderRight = DoubleAttr(e, "border-right", 0),
            Padding = DoubleAttr(e, "padding", 0),
            PadTop = DoubleAttr(e, "pad-top", 0),
            PadBottom = DoubleAttr(e, "pad-bottom", 0),
            PadLeft = DoubleAttr(e, "pad-left", 0),
            PadRight = DoubleAttr(e, "pad-right", 0),
            MinHeight = DoubleAttr(e, "min-height", 0)
        };
        ParseChildren(e, div.Children);
        return div;
    }

    private static TextNode ParseText(XElement e) {
        var text = new TextNode {
            Size = NullableDoubleAttr(e, "size"),
            Bold = BoolAttr(e, "bold", false),
            Italic = BoolAttr(e, "italic", false),
            Color = e.Attribute("color")?.Value,
            Align = e.Attribute("align")?.Value,
            PadTop = DoubleAttr(e, "pad-top", 0)
        };
        if (e.Elements("span").Any()) {
            foreach (var node in e.Nodes()) {
                if (node is XElement xe && xe.Name.LocalName == "span") {
                    text.Spans.Add(ParseSpan(xe));
                } else if (node is XText xt && !string.IsNullOrWhiteSpace(xt.Value)) {
                    text.Spans.Add(new TextSpanNode { Text = xt.Value });
                }
            }
        } else {
            text.Spans.Add(new TextSpanNode { Text = e.Value });
        }
        return text;
    }

    private static TextSpanNode ParseSpan(XElement xe) => new() {
        Text = xe.Value,
        Size = NullableDoubleAttr(xe, "size"),
        Bold = BoolAttr(xe, "bold", false),
        Italic = BoolAttr(xe, "italic", false),
        Underline = BoolAttr(xe, "underline", false),
        Color = xe.Attribute("color")?.Value
    };

    private static SvgNode ParseSvg(XElement e) => new() {
        Path = StringAttr(e, "path", ""),
        Width = NullableDoubleAttr(e, "width"),
        Height = NullableDoubleAttr(e, "height")
    };

    private static ImageNode ParseImage(XElement e) => new() {
        Path = StringAttr(e, "path", ""),
        Width = NullableDoubleAttr(e, "width"),
        Height = NullableDoubleAttr(e, "height")
    };

    private static HrNode ParseHr(XElement e) => new() {
        Thickness = DoubleAttr(e, "thickness", 0.5),
        Color = e.Attribute("color")?.Value,
        PadTop = DoubleAttr(e, "pad-top", 0),
        PadBottom = DoubleAttr(e, "pad-bottom", 0)
    };

    private static SpacerNode ParseSpacer(XElement e) => new() {
        Height = DoubleAttr(e, "height", 0),
        Width = DoubleAttr(e, "width", 0)
    };

    private static InputNode ParseInput(XElement e) => new() {
        InputType = StringAttr(e, "type", "checkbox"),
        Checked = BoolAttr(e, "checked", false),
        Size = DoubleAttr(e, "size", 11)
    };

    private static TableNode ParseTable(XElement e) {
        var table = new TableNode();

        var defs = (e.Attribute("columnDefinitions")?.Value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var raw in defs) {
            var trimmed = raw.Trim();
            if (trimmed.EndsWith("pt", StringComparison.OrdinalIgnoreCase)) {
                table.Columns.Add(new TableColumnDef { Width = double.Parse(trimmed[..^2], Inv) });
            } else {
                table.Columns.Add(new TableColumnDef { Weight = double.Parse(trimmed, Inv) });
            }
        }

        foreach (var section in e.Elements()) {
            switch (section.Name.LocalName) {
                case "thead":
                    table.Header = new TableHeaderNode();
                    foreach (var cell in FirstRowCells(section)) {
                        table.Header.Cells.Add(ParseCell(cell));
                    }
                    break;
                case "tfoot":
                    table.Footer = new TableFooterNode();
                    foreach (var cell in FirstRowCells(section)) {
                        table.Footer.Cells.Add(ParseCell(cell));
                    }
                    break;
                case "tbody":
                    foreach (var tr in section.Elements("tr")) {
                        var row = new TableRowNode();
                        foreach (var cell in tr.Elements().Where(IsCell)) {
                            row.Cells.Add(ParseCell(cell));
                        }
                        table.Rows.Add(row);
                    }
                    break;
            }
        }
        return table;
    }

    private static System.Collections.Generic.IEnumerable<XElement> FirstRowCells(XElement section) {
        var tr = section.Elements("tr").FirstOrDefault();
        return tr == null
            ? Enumerable.Empty<XElement>()
            : tr.Elements().Where(IsCell);
    }

    private static bool IsCell(XElement e) => e.Name.LocalName == "td" || e.Name.LocalName == "th";

    private static TableCellNode ParseCell(XElement e) {
        var cell = new TableCellNode {
            ColSpan = (int)DoubleAttr(e, "colspan", 1),
            Background = e.Attribute("background")?.Value,
            BorderWidth = DoubleAttr(e, "border", 0.5),
            BorderColor = e.Attribute("border-color")?.Value,
            PadVertical = DoubleAttr(e, "pad-vertical", 2),
            PadHorizontal = DoubleAttr(e, "pad-horizontal", 6),
            MinHeight = DoubleAttr(e, "min-height", 14),
            Align = e.Attribute("align")?.Value,
            VerticalAlign = e.Attribute("valign")?.Value ?? "middle"
        };

        var childElements = e.Elements().ToList();
        var allInline = childElements.Count > 0 && childElements.All(c => c.Name.LocalName == "span");
        if (childElements.Count > 0 && !allInline) {
            ParseChildren(e, cell.Children);
        } else if (allInline) {
            var bold = BoolAttr(e, "bold", false);
            var italic = BoolAttr(e, "italic", false);
            var text = new TextNode { Bold = bold, Italic = italic, Align = cell.Align };
            foreach (var node in e.Nodes()) {
                if (node is XElement xe && xe.Name.LocalName == "span") {
                    text.Spans.Add(ParseSpan(xe));
                } else if (node is XText xt && !string.IsNullOrWhiteSpace(xt.Value)) {
                    text.Spans.Add(new TextSpanNode { Text = xt.Value });
                }
            }
            cell.Children.Add(text);
        } else if (!string.IsNullOrWhiteSpace(e.Value)) {
            var bold = BoolAttr(e, "bold", false);
            var italic = BoolAttr(e, "italic", false);
            var text = new TextNode { Bold = bold, Italic = italic, Align = cell.Align };
            text.Spans.Add(new TextSpanNode { Text = e.Value, Bold = bold, Italic = italic });
            cell.Children.Add(text);
        }
        return cell;
    }

    private static TextNode WrapText(string raw) {
        var text = new TextNode();
        text.Spans.Add(new TextSpanNode { Text = raw.Trim() });
        return text;
    }

    private static string StringAttr(XElement e, string name, string fallback)
        => e.Attribute(name)?.Value ?? fallback;

    private static double DoubleAttr(XElement e, string name, double fallback) {
        var v = e.Attribute(name)?.Value;
        return v == null ? fallback : double.Parse(v, Inv);
    }

    private static double? NullableDoubleAttr(XElement e, string name) {
        var v = e.Attribute(name)?.Value;
        return v == null ? null : double.Parse(v, Inv);
    }

    private static bool BoolAttr(XElement e, string name, bool fallback) {
        var v = e.Attribute(name)?.Value;
        if (v == null) return fallback;
        return v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "1";
    }
}
