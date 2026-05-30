using Microsoft.AspNetCore.Components;

namespace Quartermaster.Blazor.Components;

public partial class EmptyState {
    /// <summary>
    /// The empty-state text to display, e.g. <c>I18n[I18nKey.Ui.Common.NoEntries]</c>.
    /// </summary>
    [Parameter, EditorRequired]
    public string Message { get; set; } = "";
}
