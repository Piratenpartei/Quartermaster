using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Pages.DueSelector;

public partial class DueSelectionPersonalDataStep {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public DueSelectorEntryState State { get; set; } = default!;

    /// <summary>Logged-in members get name and email from their account and can't change them here.</summary>
    [Parameter]
    public bool Locked { get; set; }
}
