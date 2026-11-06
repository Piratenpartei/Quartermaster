using Microsoft.AspNetCore.Components;

namespace Quartermaster.Blazor.Components;

/// <summary>The header of every page: optional back link, title, an optional line below it, and the page's actions, which wrap below the title on a phone.</summary>
public partial class PageHeader {
    [Parameter, EditorRequired]
    public string Title { get; set; } = "";

    /// <summary>Optional Bootstrap Icons class shown before the title, for example <c>bi-signpost-split</c>.</summary>
    [Parameter]
    public string? Icon { get; set; }

    [Parameter]
    public string? BackHref { get; set; }

    /// <summary>Overrides the back link's default "Zurück zur Übersicht".</summary>
    [Parameter]
    public string? BackText { get; set; }

    /// <summary>The line below the title, for example chapter, date or status badges.</summary>
    [Parameter]
    public RenderFragment? Subtitle { get; set; }

    [Parameter]
    public RenderFragment? Actions { get; set; }

    /// <summary>Small buttons to linked pages, in the back link's row; needs <see cref="BackHref"/>.</summary>
    [Parameter]
    public RenderFragment? Links { get; set; }
}
