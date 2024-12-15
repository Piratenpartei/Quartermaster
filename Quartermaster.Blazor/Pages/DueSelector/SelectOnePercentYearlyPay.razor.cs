using Microsoft.AspNetCore.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.DueSelector;

public partial class SelectOnePercentYearlyPay {
    [Inject]
    public required AppStateService AppState { get; set; }

    private DueSelectorEntryState? EntryState;

    protected override void OnInitialized() {
        EntryState = AppState.GetEntryState<DueSelectorEntryState>();
    }

    private decimal CalculatedPercentOfIncome() {
        if (EntryState != null) {
            EntryState.SelectedDue = EntryState.YearlyIncome * 0.01m;
            return EntryState.SelectedDue;
        } else {
            return 0;
        }
    }
}