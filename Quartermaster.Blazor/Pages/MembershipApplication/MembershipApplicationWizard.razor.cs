using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
    private static readonly string[] RestartCodeStepErrors = [
        I18nKey.Error.EmailCode.Expired,
        I18nKey.Error.EmailCode.Locked,
        I18nKey.Error.EmailCode.Wrong,
        I18nKey.Error.EmailCode.Required
    ];

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
    private FormWizard? Wizard;
    private MembershipEmailCodeStep? CodeStep;
    private int CodeStepIndex = -1;

    protected override async Task OnInitializedAsync() {
        State = AppState.GetEntryState<MembershipApplicationEntryState>();
        DuesState = AppState.GetEntryState<DueSelectorEntryState>();
        SyncDuesState();
        await AuthService.WaitForInitializationAsync();
        Steps = BuildSteps();
    }

    private List<WizardStep> BuildSteps() {
        var steps = new PublicFormStepBuilder(I18n, ToastService);
        List<WizardStep> list = [
            steps.StateStep<MembershipPersonalDataStep>(I18nKey.Ui.PublicFormWizard.StepPersonalData, State, State.IsPersonalDataComplete,
                I18nKey.Ui.PublicFormWizard.PersonalDataRequired),
            steps.StateStep<MembershipAddressStep>(I18nKey.Ui.PublicFormWizard.StepAddress, State, State.IsAddressComplete,
                I18nKey.Ui.PublicFormWizard.AddressRequired),
            steps.StateStep<DuesStep>(I18nKey.Ui.PublicFormWizard.StepDues, DuesState, DuesState.IsDuesComplete, I18nKey.Ui.PublicFormWizard.DuesRequired),
            steps.StateStep<PaymentStep>(I18nKey.Ui.PublicFormWizard.StepPayment, DuesState, DuesState.IsPaymentComplete,
                I18nKey.Ui.PublicFormWizard.PaymentRequired),
            steps.StateStep<MembershipDeclarationsStep>(I18nKey.Ui.PublicFormWizard.StepDeclarations, State, State.AreDeclarationsComplete,
                I18nKey.Ui.PublicFormWizard.DeclarationsRequired)
        ];
        CodeStepIndex = -1;
        if (!AuthService.IsAuthenticated) {
            CodeStepIndex = list.Count;
            list.Add(BuildEmailCodeStep());
        }
        list.Add(steps.Step(I18nKey.Ui.PublicFormWizard.StepSummary, builder => {
            builder.OpenComponent<MembershipOverviewStep>(0);
            builder.AddComponentParameter(1, nameof(MembershipOverviewStep.State), State);
            builder.AddComponentParameter(2, nameof(MembershipOverviewStep.DuesState), DuesState);
            builder.CloseComponent();
        }, () => true, null));
        return list;
    }

    private WizardStep BuildEmailCodeStep() {
        return new WizardStep {
            Title = I18n[I18nKey.Ui.PublicFormWizard.StepEmailCode],
            Content = builder => {
                builder.OpenComponent<MembershipEmailCodeStep>(0);
                builder.AddComponentParameter(1, PublicFormStepBuilder.StateParameter, State);
                builder.AddComponentReferenceCapture(2, reference => CodeStep = (MembershipEmailCodeStep)reference);
                builder.CloseComponent();
            },
            IsValid = State.IsEmailCodeComplete,
            Validate = () => CodeStep == null ? Task.FromResult(State.IsEmailCodeComplete()) : CodeStep.ValidateAsync()
        };
    }

    // The dues part is a due selection of its own and carries the applicant's name for the account holder and the summary.
    private void SyncDuesState() {
        DuesState.FirstName = State.FirstName;
        DuesState.LastName = State.LastName;
        DuesState.Email = State.Email;
    }

    private static async Task<bool> IsEmailCodeRefusalAsync(HttpResponseMessage response) {
        if (response.StatusCode != HttpStatusCode.BadRequest)
            return false;

        var body = await response.Content.ReadAsStringAsync();
        return RestartCodeStepErrors.Any(body.Contains);
    }

    private async Task RestartEmailCodeStepAsync() {
        if (Wizard == null || CodeStepIndex < 0)
            return;

        State.EmailCode = "";
        State.EmailCodeVerifiedFor = null;
        State.EmailCodeSentAt = null;
        State.EmailCodeExpiresAt = null;
        await Wizard.ShowStepAsync(CodeStepIndex);
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
            AddressCountry = State.IsGermany ? "" : State.AddressCountry,
            AddressAdministrativeDivisionId = State.AddressAdministrativeDivisionId,
            ChapterId = State.ChapterId,
            DueSelection = DuesState.ToDTO(),
            ConformityDeclarationAccepted = State.ConformityDeclarationAccepted,
            HasPriorDeclinedApplication = State.HasPriorDeclinedApplication,
            IsMemberOfAnotherParty = State.IsMemberOfAnotherParty,
            OtherParties = State.IsMemberOfAnotherParty ? State.OtherParties : "",
            ApplicationText = State.ApplicationText,
            EntryDate = DateOnly.FromDateTime(State.EntryDate),
            EmailVerificationCode = State.EmailCode
        };

        try {
            var result = await Http.PostAsJsonAsync("/api/membershipapplications", dto);
            if (!result.IsSuccessStatusCode) {
                var codeRefused = await IsEmailCodeRefusalAsync(result);
                await ToastService.ErrorAsync(result);
                if (codeRefused)
                    await RestartEmailCodeStepAsync();
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
