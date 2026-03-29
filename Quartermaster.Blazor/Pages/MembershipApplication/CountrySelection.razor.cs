using Microsoft.AspNetCore.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.MembershipApplication;

public partial class CountrySelection {
    [Inject]
    public required AppStateService AppState { get; set; }

    private MembershipApplicationEntryState? EntryState;

    protected override void OnInitialized() {
        EntryState = AppState.GetEntryState<MembershipApplicationEntryState>();
    }

    private void SelectCountry(bool isGermany) {
        if (EntryState == null)
            return;

        EntryState.IsGermany = isGermany;
        EntryState.AddressCountry = isGermany ? "Deutschland" : "";

        // Reset address fields when changing country
        EntryState.AddressPostCode = "";
        EntryState.AddressCity = "";
        EntryState.AddressStreet = "";
        EntryState.AddressHouseNbr = "";
        EntryState.AddressAdministrativeDivisionId = null;
        EntryState.ChapterId = null;
        EntryState.ChapterName = null;
    }
}
