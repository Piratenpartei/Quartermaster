using Microsoft.AspNetCore.Components;

namespace Quartermaster.Blazor.Components;

public partial class EmptyState {
    /// <summary>
    /// The German empty-state text to display, e.g. "Keine Einträge vorhanden."
    /// </summary>
    [Parameter, EditorRequired]
    public string Message { get; set; } = "";
}
