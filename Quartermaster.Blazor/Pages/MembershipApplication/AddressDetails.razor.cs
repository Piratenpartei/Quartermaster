using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.MembershipApplication;

public partial class AddressDetails {
    [Inject]
    public required AppStateService AppState { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }

    private MembershipApplicationEntryState? EntryState;

    private string BackHref => EntryState != null && EntryState.IsGermany
        ? "/MembershipApplication/Address"
        : "/MembershipApplication/CountrySelection";

    private bool FromMunicipality => EntryState?.AddressAdministrativeDivisionId != null;

    protected override void OnInitialized() {
        EntryState = AppState.GetEntryState<MembershipApplicationEntryState>();
    }

    private bool CanContinue() {
        if (EntryState == null)
            return false;
        if (string.IsNullOrEmpty(EntryState.AddressPostCode))
            return false;
        if (string.IsNullOrEmpty(EntryState.AddressCity))
            return false;
        if (string.IsNullOrEmpty(EntryState.AddressStreet))
            return false;
        if (string.IsNullOrEmpty(EntryState.AddressHouseNbr))
            return false;
        if (!EntryState.IsGermany && string.IsNullOrEmpty(EntryState.AddressCountry))
            return false;
        return true;
    }
}
