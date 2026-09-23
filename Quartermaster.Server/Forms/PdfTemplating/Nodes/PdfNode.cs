using System.Collections.Generic;

namespace Quartermaster.Server.Forms.PdfTemplating.Nodes;

public abstract class PdfNode { }

public abstract class PdfContainerNode : PdfNode {
    public List<PdfNode> Children { get; } = new();
}

public sealed class PageNode : PdfContainerNode {
    public string Size { get; set; } = "A4";
    public double Margin { get; set; } = 36;
    public string FontFamily { get; set; } = "Helvetica";
    public double FontSize { get; set; } = 10;
}

public sealed class ColumnNode : PdfContainerNode {
    public double Spacing { get; set; }
    public double PadTop { get; set; }
    public double PadBottom { get; set; }
    public double PadLeft { get; set; }
    public double PadRight { get; set; }
}

public sealed class RowNode : PdfContainerNode {
    public double Spacing { get; set; }
    public double PadTop { get; set; }
}

/// <summary>Generic block container (<c>&lt;div&gt;</c>). As a direct child of a <see cref="RowNode"/> it becomes a flex item sized by <see cref="Width"/> or <see cref="Weight"/>; elsewhere it is a plain block. Carries border/background/padding so it also covers boxed regions.</summary>
public sealed class DivNode : PdfContainerNode {
    public double? Width { get; set; }
    public double Weight { get; set; } = 1;
    public string? Align { get; set; }
    public string? VerticalAlign { get; set; }
    public string? Background { get; set; }
    public string? BorderColor { get; set; }
    public double Border { get; set; }
    public double BorderTop { get; set; }
    public double BorderBottom { get; set; }
    public double BorderLeft { get; set; }
    public double BorderRight { get; set; }
    public double Padding { get; set; }
    public double PadTop { get; set; }
    public double PadBottom { get; set; }
    public double PadLeft { get; set; }
    public double PadRight { get; set; }
    public double MinHeight { get; set; }
}

public sealed class TextNode : PdfNode {
    public double? Size { get; set; }
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public string? Color { get; set; }
    public string? Align { get; set; }
    public double PadTop { get; set; }
    public List<TextSpanNode> Spans { get; } = new();
}

public sealed class TextSpanNode {
    public string Text { get; set; } = "";
    public double? Size { get; set; }
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Underline { get; set; }
    public string? Color { get; set; }
}

public sealed class SvgNode : PdfNode {
    public string Path { get; set; } = "";
    public double? Width { get; set; }
    public double? Height { get; set; }
}

public sealed class ImageNode : PdfNode {
    public string Path { get; set; } = "";
    public double? Width { get; set; }
    public double? Height { get; set; }
}

public sealed class HrNode : PdfNode {
    public double Thickness { get; set; } = 0.5;
    public string? Color { get; set; }
    public double PadTop { get; set; }
    public double PadBottom { get; set; }
}

public sealed class SpacerNode : PdfNode {
    public double Height { get; set; }
    public double Width { get; set; }
}

public sealed class InputNode : PdfNode {
    /// <summary>HTML-style input type. Only <c>checkbox</c> is rendered today; text inputs are a future extension.</summary>
    public string InputType { get; set; } = "checkbox";
    public bool Checked { get; set; }
    public double Size { get; set; } = 11;
}

public sealed class TableNode : PdfNode {
    public List<TableColumnDef> Columns { get; } = new();
    public TableHeaderNode? Header { get; set; }
    public TableFooterNode? Footer { get; set; }
    public List<TableRowNode> Rows { get; } = new();
}

public sealed class TableColumnDef {
    public double? Width { get; set; }
    public double Weight { get; set; } = 1;
}

public sealed class TableHeaderNode {
    public List<TableCellNode> Cells { get; } = new();
}

public sealed class TableFooterNode {
    public List<TableCellNode> Cells { get; } = new();
}

public sealed class TableRowNode {
    public List<TableCellNode> Cells { get; } = new();
}

public sealed class TableCellNode : PdfContainerNode {
    public int ColSpan { get; set; } = 1;
    public string? Background { get; set; }
    public double BorderWidth { get; set; } = 0.5;
    public string? BorderColor { get; set; }
    public double PadVertical { get; set; } = 2;
    public double PadHorizontal { get; set; } = 6;
    public double MinHeight { get; set; } = 14;
    public string? Align { get; set; }
    public string? VerticalAlign { get; set; } = "middle";
}
