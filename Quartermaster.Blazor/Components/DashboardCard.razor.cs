using Microsoft.AspNetCore.Components;

namespace Quartermaster.Blazor.Components;

public partial class DashboardCard {
    /// <summary>
    /// Bootstrap icon class (e.g., "bi-person-plus"). Optional.
    /// </summary>
    [Parameter]
    public string Icon { get; set; } = "";

    /// <summary>
    /// Card title shown next to the icon.
    /// </summary>
    [Parameter, EditorRequired]
    public string Title { get; set; } = "";

    /// <summary>
    /// Optional total count shown as a secondary badge.
    /// </summary>
    [Parameter]
    public int? TotalCount { get; set; }

    /// <summary>
    /// URL for the "show all" link in the header.
    /// </summary>
    [Parameter, EditorRequired]
    public string AllUrl { get; set; } = "";

    /// <summary>
    /// Table or other content rendered inside the card body.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
