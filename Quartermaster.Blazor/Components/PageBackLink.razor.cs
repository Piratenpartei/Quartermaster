using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components;

public partial class PageBackLink {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public string Href { get; set; } = "";

    /// <summary>Overrides the default <c>ui.label.back_to_overview</c> translation.</summary>
    [Parameter]
    public string? Text { get; set; }

    /// <summary>Small buttons to linked pages, on the right of the back link.</summary>
    [Parameter]
    public RenderFragment? Links { get; set; }

    private string ResolvedText => Text ?? I18n.Translate(I18nKey.Ui.Label.BackToOverview);
}
