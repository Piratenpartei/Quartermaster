using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Submissions;
using Quartermaster.Api.I18n;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Blazor.Components.Forms;
using Quartermaster.Blazor.Pages.DueSelector;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.MembershipApplication;

public partial class MembershipApplicationWizard {
    [Inject]
    public required AppStateService AppState { get; set; }
    [Inject]
    public required AuthService AuthService { get; set; }
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }

    private MembershipApplicationEntryState State = default!;
    private DueSelectorEntryState DuesState = default!;
    private List<WizardStep> Steps = [];
    private string? SubmittedEmail;
    private bool SubmittedDirect;

    protected override async Task OnInitializedAsync() {
        State = AppState.GetEntryState<MembershipApplicationEntryState>();
        DuesState = AppState.GetEntryState<DueSelectorEntryState>();
        SyncDuesState();
        await AuthService.WaitForInitializationAsync();
        Steps = BuildSteps();
    }

    private List<WizardStep> BuildSteps() {
        var steps = new PublicFormStepBuilder(I18n, ToastService);
        return [
            steps.StateStep<MembershipPersonalDataStep>(I18nKey.Ui.PublicFormWizard.StepPersonalData, State, State.IsPersonalDataComplete,
                I18nKey.Ui.PublicFormWizard.PersonalDataRequired),
            steps.StateStep<MembershipAddressStep>(I18nKey.Ui.PublicFormWizard.StepAddress, State, State.IsAddressComplete,
                I18nKey.Ui.PublicFormWizard.AddressRequired),
            steps.StateStep<DuesStep>(I18nKey.Ui.PublicFormWizard.StepDues, DuesState, DuesState.IsDuesComplete, I18nKey.Ui.PublicFormWizard.DuesRequired),
            steps.StateStep<PaymentStep>(I18nKey.Ui.PublicFormWizard.StepPayment, DuesState, DuesState.IsPaymentComplete,
                I18nKey.Ui.PublicFormWizard.PaymentRequired),
            steps.StateStep<MembershipDeclarationsStep>(I18nKey.Ui.PublicFormWizard.StepDeclarations, State, State.AreDeclarationsComplete,
                I18nKey.Ui.PublicFormWizard.DeclarationsRequired),
            steps.Step(I18nKey.Ui.PublicFormWizard.StepSummary, builder => {
                builder.OpenComponent<MembershipOverviewStep>(0);
                builder.AddComponentParameter(1, nameof(MembershipOverviewStep.State), State);
                builder.AddComponentParameter(2, nameof(MembershipOverviewStep.DuesState), DuesState);
                builder.CloseComponent();
            }, () => true, null)
        ];
    }

    // The dues part is a due selection of its own and carries the applicant's name for the account holder and the summary.
    private void SyncDuesState() {
        DuesState.FirstName = State.FirstName;
        DuesState.LastName = State.LastName;
        DuesState.Email = State.Email;
    }

    private async Task Submit() {
        SyncDuesState();
        DuesState.RecalculateDue();
        var dto = new MembershipApplicationDTO {
            FirstName = State.FirstName,
            LastName = State.LastName,
            DateOfBirth = DateOnly.FromDateTime(State.DateOfBirth ?? DateTime.MinValue),
            Citizenship = State.Citizenship,
            Email = State.Email,
            PhoneNumber = State.PhoneNumber,
            AddressStreet = State.AddressStreet,
            AddressHouseNbr = State.AddressHouseNbr,
            AddressPostCode = State.AddressPostCode,
            AddressCity = State.AddressCity,
            AddressAdministrativeDivisionId = State.AddressAdministrativeDivisionId,
            ChapterId = State.ChapterId,
            DueSelection = DuesState.ToDTO(),
            ConformityDeclarationAccepted = State.ConformityDeclarationAccepted,
            HasPriorDeclinedApplication = State.HasPriorDeclinedApplication,
            IsMemberOfAnotherParty = State.IsMemberOfAnotherParty,
            ApplicationText = State.ApplicationText,
            EntryDate = DateOnly.FromDateTime(State.EntryDate)
        };

        try {
            var result = await Http.PostAsJsonAsync("/api/membershipapplications", dto);
            if (!result.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(result);
                return;
            }

            var body = await result.Content.ReadFromJsonAsync<SubmissionAcceptedResponse>();
            if (body?.RequiresConfirmation == false) {
                SubmittedDirect = true;
            } else {
                SubmittedEmail = State.Email;
            }
            AppState.ResetEntryState<MembershipApplicationEntryState>();
            AppState.ResetEntryState<DueSelectorEntryState>();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }
}
