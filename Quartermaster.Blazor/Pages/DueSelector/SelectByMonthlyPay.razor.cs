using Microsoft.AspNetCore.Components;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.DueSelector;

public partial class SelectByMonthlyPay {
    [Inject]
    public required AppStateService AppState { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }

    private DueSelectorEntryState? EntryState;
    protected override void OnInitialized() {
        EntryState = AppState.GetEntryState<DueSelectorEntryState>();
    }

    private decimal CalculateDues() {
        if (EntryState == null)
            return 72;

        EntryState.SelectedDue = EntryState.MonthlyIncomeGroup switch {
            >= 6000 => 600,
            >= 5000 => 480,
            >= 4000 => 360,
            >= 3000 => 240,
            >= 2500 => 180,
            >= 2000 => 120,
            _ => 72
        };

        return EntryState.SelectedDue;
    }

    private string GetPreviousUrl() {
        if(EntryState != null && EntryState.SelectedValuation == SelectedValuation.Reduced) {
            return "/DueSelector/SelectReduced";
        } else {
            return "/DueSelector/DueTypeSelector";
        }
    }
}