using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components;

public partial class DeleteButton {
    /// <summary>
    /// Click handler — typically opens a ConfirmDialog and then performs the delete.
    /// </summary>
    [Parameter, EditorRequired]
    public EventCallback OnClick { get; set; }

    /// <summary>
    /// Visible button text. Defaults to the localized "Delete". Pass an empty string for an
    /// icon-only button (and set <see cref="AriaLabel"/> for screen readers).
    /// </summary>
    [Parameter]
    public string? Text { get; set; }

    /// <summary>
    /// Accessible label for icon-only buttons. Defaults to the localized "Delete".
    /// </summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// Use the small button variant (btn-sm). Default true to match most usages.
    /// </summary>
    [Parameter]
    public bool Small { get; set; } = true;

    /// <summary>
    /// Extra CSS classes to add to the button (e.g., "ms-auto").
    /// </summary>
    [Parameter]
    public string CssClass { get; set; } = "";

    /// <summary>
    /// Disable the button (e.g., during a delete operation).
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    private string ResolvedText => Text ?? I18n[I18nKey.Ui.DeleteButton.DefaultText];
    private string ResolvedAriaLabel => AriaLabel ?? I18n[I18nKey.Ui.DeleteButton.DefaultText];
}
