using Microsoft.AspNetCore.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.MembershipApplication;

public partial class Declarations {
    [Inject]
    public required AppStateService AppState { get; set; }

    private MembershipApplicationEntryState? EntryState;

    private bool NoPriorDeclinedApplication {
        get => EntryState != null && !EntryState.HasPriorDeclinedApplication;
        set {
            if (EntryState != null)
                EntryState.HasPriorDeclinedApplication = !value;
        }
    }

    protected override void OnInitialized() {
        EntryState = AppState.GetEntryState<MembershipApplicationEntryState>();
    }

    private bool CanContinue() {
        if (EntryState == null)
            return false;
        if (!EntryState.ConformityDeclarationAccepted)
            return false;
        return true;
    }
}
