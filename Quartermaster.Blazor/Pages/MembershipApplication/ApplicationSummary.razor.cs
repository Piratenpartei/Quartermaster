using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Submissions;
using Quartermaster.Blazor.Pages.DueSelector;
using Quartermaster.Blazor.Services;

using Quartermaster.Api.DueSelector;

namespace Quartermaster.Blazor.Pages.MembershipApplication;

public partial class ApplicationSummary {
    [Inject]
    public required AppStateService AppState { get; set; }
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }

    private MembershipApplicationEntryState? EntryState;
    private DueSelectorEntryState? DuesState;
    private string? SubmittedEmail;
    private bool SubmittedDirect;

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
            DateOfBirth = DateOnly.FromDateTime(EntryState.DateOfBirth ?? DateTime.MinValue),
            Citizenship = EntryState.Citizenship,
            Email = EntryState.Email,
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
            EntryDate = DateOnly.FromDateTime(EntryState.EntryDate)
        };

        try {
            var result = await Http.PostAsJsonAsync("/api/membershipapplications", dto);
            if (result.IsSuccessStatusCode) {
                var body = await result.Content.ReadFromJsonAsync<SubmissionAcceptedResponse>();
                if (body?.RequiresConfirmation == false) {
                    SubmittedDirect = true;
                } else {
                    SubmittedEmail = EntryState.Email;
                }
                AppState.ResetEntryState<MembershipApplicationEntryState>();
                AppState.ResetEntryState<DueSelectorEntryState>();
                StateHasChanged();
            } else {
                await ToastService.ErrorAsync(result);
            }
        } catch (HttpRequestException) {
            ToastService.Error();
        }
    }
}
