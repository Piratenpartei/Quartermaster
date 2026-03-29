using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Blazor.Pages.DueSelector;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.MembershipApplication;

public partial class ApplicationSummary {
    [Inject]
    public required AppStateService AppState { get; set; }
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required NavigationManager NavigationManager { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    private MembershipApplicationEntryState? EntryState;
    private DueSelectorEntryState? DuesState;

    protected override void OnInitialized() {
        EntryState = AppState.GetEntryState<MembershipApplicationEntryState>();
        DuesState = AppState.GetEntryState<DueSelectorEntryState>();
    }

    private async Task Submit() {
        if (EntryState == null)
            throw new UnreachableException();

        var dto = new MembershipApplicationDTO {
            FirstName = EntryState.FirstName,
            LastName = EntryState.LastName,
            DateOfBirth = EntryState.DateOfBirth ?? DateTime.MinValue,
            Citizenship = EntryState.Citizenship,
            EMail = EntryState.EMail,
            PhoneNumber = EntryState.PhoneNumber,
            AddressStreet = EntryState.AddressStreet,
            AddressHouseNbr = EntryState.AddressHouseNbr,
            AddressPostCode = EntryState.AddressPostCode,
            AddressCity = EntryState.AddressCity,
            AddressAdministrativeDivisionId = EntryState.AddressAdministrativeDivisionId,
            ChapterId = EntryState.ChapterId,
            DueSelection = DuesState?.ToDTO(),
            ConformityDeclarationAccepted = EntryState.ConformityDeclarationAccepted,
            HasPriorDeclinedApplication = EntryState.HasPriorDeclinedApplication,
            IsMemberOfAnotherParty = EntryState.IsMemberOfAnotherParty,
            ApplicationText = EntryState.ApplicationText,
            EntryDate = EntryState.EntryDate
        };

        var okResponse = false;
        try {
            var result = await Http.PostAsJsonAsync("/api/membershipapplications", dto);
            okResponse = result.IsSuccessStatusCode;
        } catch (HttpRequestException) { }

        if (okResponse) {
            AppState.ResetEntryState<MembershipApplicationEntryState>();
            AppState.ResetEntryState<DueSelectorEntryState>();
            NavigationManager.NavigateTo("/");
            ToastService.Toast("Dein Mitgliedsantrag wurde erfolgreich eingereicht!", "success");
        } else {
            ToastService.Toast("Es ist ein Fehler aufgetreten, bitte versuche es später nochmal erneut.", "danger");
        }
    }
}
