using Microsoft.AspNetCore.Components;

namespace Quartermaster.Blazor.Components;

public partial class PageHeader {
    /// <summary>
    /// Page title shown on the left.
    /// </summary>
    [Parameter, EditorRequired]
    public string Title { get; set; } = "";

    /// <summary>
    /// Optional action buttons rendered on the right inside a flex gap-2 wrapper.
    /// </summary>
    [Parameter]
    public RenderFragment? Actions { get; set; }
}
