namespace Quartermaster.Blazor.Components;

public class Toast {
    public string Content { get; set; } = "";
    public string Type { get; set; } = "";
    public string? Details { get; set; }
    /// <summary>
    /// Auto-dismiss duration in milliseconds. Null means persistent (manual dismiss only).
    /// </summary>
    public int? DurationMs { get; set; }
}