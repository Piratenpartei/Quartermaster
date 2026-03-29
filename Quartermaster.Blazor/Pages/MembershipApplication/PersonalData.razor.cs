using Microsoft.AspNetCore.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.MembershipApplication;

public partial class PersonalData {
    [Inject]
    public required AppStateService AppState { get; set; }

    private MembershipApplicationEntryState? EntryState;

    protected override void OnInitialized() {
        EntryState = AppState.GetEntryState<MembershipApplicationEntryState>();
    }

    private bool CanContinue() {
        if (EntryState == null)
            return false;
        if (string.IsNullOrEmpty(EntryState.FirstName))
            return false;
        if (string.IsNullOrEmpty(EntryState.LastName))
            return false;
        if (EntryState.DateOfBirth == null)
            return false;
        if (string.IsNullOrEmpty(EntryState.Citizenship))
            return false;
        if (string.IsNullOrEmpty(EntryState.EMail))
            return false;
        return true;
    }
}
