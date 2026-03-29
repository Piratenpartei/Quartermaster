using Microsoft.AspNetCore.Components;
using Quartermaster.Blazor.Pages.DueSelector;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.MembershipApplication;

public partial class DuesTypeSelection {
    [Inject]
    public required AppStateService AppState { get; set; }

    private MembershipApplicationEntryState? EntryState;
    private DueSelectorEntryState? DuesState;

    protected override void OnInitialized() {
        EntryState = AppState.GetEntryState<MembershipApplicationEntryState>();
        DuesState = AppState.GetEntryState<DueSelectorEntryState>();
        SyncToDuesState();
    }

    private void SyncToDuesState() {
        if (EntryState == null || DuesState == null)
            return;
        DuesState.FirstName = EntryState.FirstName;
        DuesState.LastName = EntryState.LastName;
        DuesState.EMail = EntryState.EMail;
    }

    private void SelectDueType(SelectedValuation valuation) {
        if (DuesState == null)
            return;
        DuesState.SelectedValuation = valuation;
    }

    private void HandleUnderage() {
        if (DuesState == null)
            return;
        DuesState.SelectedValuation = SelectedValuation.Underage;
        DuesState.SelectedDue = 12;
    }
}
