using Microsoft.AspNetCore.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.DueSelector;

public partial class UserDataInput {
    [Inject]
    public required AppStateService AppState { get; set; }

    private DueSelectorEntryState? EntryState;

    protected override void OnInitialized() {
        EntryState = AppState.GetEntryState<DueSelectorEntryState>();
    }

    private bool CanContinue() {
        if (EntryState == null)
            return false;

        if (string.IsNullOrEmpty(EntryState.FirstName))
            return false;
        if (string.IsNullOrEmpty(EntryState.LastName))
            return false;
        if (string.IsNullOrEmpty(EntryState.Email))
            return false;
        if (EntryState.MemberNumber == 0)
            return false;

        return true;
    }
}