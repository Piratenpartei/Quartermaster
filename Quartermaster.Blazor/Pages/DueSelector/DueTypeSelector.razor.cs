using Microsoft.AspNetCore.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.DueSelector;

public partial class DueTypeSelector {
    [Inject]
    public required AppStateService AppState { get; set; }

    private DueSelectorEntryState? EntryState;

    protected override void OnInitialized() {
        EntryState = AppState.GetEntryState<DueSelectorEntryState>();
    }

    private void HandleUnderage(SelectedValuation selectedValuation) {
        SelectDueType(selectedValuation);
        if (EntryState != null) {
            EntryState.SelectedDue = 12;
        }
    }

    private void SelectDueType(SelectedValuation selectedValuation) {
        if (EntryState == null)
            return;

        EntryState.SelectedValuation = selectedValuation;
    }
}