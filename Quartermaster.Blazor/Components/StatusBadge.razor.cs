using Microsoft.AspNetCore.Components;

namespace Quartermaster.Blazor.Components;

/// <summary>One status in Bootstrap's soft colours; every status badge of the application renders through this.</summary>
public partial class StatusBadge {
    [Parameter, EditorRequired]
    public StatusTone Tone { get; set; }

    [Parameter, EditorRequired]
    public string Text { get; set; } = "";

    /// <summary>Optional Bootstrap Icons name without the <c>bi-</c> prefix.</summary>
    [Parameter]
    public string? Icon { get; set; }

    /// <summary>Extra classes, for example spacing.</summary>
    [Parameter]
    public string Class { get; set; } = "";

    private string ToneClasses => Tone switch {
        StatusTone.Success => "bg-success-subtle text-success-emphasis border-success-subtle",
        StatusTone.Warning => "bg-warning-subtle text-warning-emphasis border-warning-subtle",
        StatusTone.Danger => "bg-danger-subtle text-danger-emphasis border-danger-subtle",
        StatusTone.Info => "bg-info-subtle text-info-emphasis border-info-subtle",
        _ => "bg-secondary-subtle text-secondary-emphasis border-secondary-subtle"
    };
}
