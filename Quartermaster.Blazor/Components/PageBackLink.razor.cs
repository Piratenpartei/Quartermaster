using Microsoft.AspNetCore.Components;

namespace Quartermaster.Blazor.Components;

public partial class PageBackLink {
    /// <summary>
    /// Target URL for the back link.
    /// </summary>
    [Parameter, EditorRequired]
    public string Href { get; set; } = "";

    /// <summary>
    /// Link text. Defaults to "Zurück zur Übersicht".
    /// </summary>
    [Parameter]
    public string Text { get; set; } = "Zurück zur Übersicht";
}
