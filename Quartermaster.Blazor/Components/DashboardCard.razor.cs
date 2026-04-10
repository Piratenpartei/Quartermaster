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
    /// URL for the "Alle anzeigen" link in the header.
    /// </summary>
    [Parameter, EditorRequired]
    public string AllUrl { get; set; } = "";

    /// <summary>
    /// Whether the widget should show its empty state instead of the table content.
    /// </summary>
    [Parameter]
    public bool IsEmpty { get; set; }

    /// <summary>
    /// German empty-state message shown when <see cref="IsEmpty"/> is true.
    /// </summary>
    [Parameter]
    public string EmptyMessage { get; set; } = "";

    /// <summary>
    /// Table or other content rendered inside the card body when not empty.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
