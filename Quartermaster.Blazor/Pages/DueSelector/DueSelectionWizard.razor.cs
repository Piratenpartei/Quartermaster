using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.Submissions;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Components.Forms;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.DueSelector;

public partial class DueSelectionWizard {
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
    [Inject]
    public required NavigationManager Navigation { get; set; }

    private DueSelectorEntryState State = default!;
    private List<WizardStep> Steps = [];
    private string? SubmittedEmail;
    private bool SubmittedDirect;
    private bool MemberLocked;

    protected override async Task OnInitializedAsync() {
        State = AppState.GetEntryState<DueSelectorEntryState>();
        await AuthService.WaitForInitializationAsync();
        if (!AuthService.IsAuthenticated)
            return;

        await PrefillAsync();
        Steps = BuildSteps();
    }

    private async Task PrefillAsync() {
        try {
            var prefill = await Http.GetFromJsonAsync<DueSelectionPrefillDTO>("/api/dueselector/prefill");
            if (prefill == null)
                return;

            MemberLocked = prefill.Locked;
            if (prefill.Locked || string.IsNullOrWhiteSpace(State.FirstName))
                State.FirstName = prefill.FirstName;
            if (prefill.Locked || string.IsNullOrWhiteSpace(State.LastName))
                State.LastName = prefill.LastName;
            if (prefill.Locked || string.IsNullOrWhiteSpace(State.Email))
                State.Email = prefill.Email;
            if (prefill.MemberNumber != null)
                State.MemberNumber = prefill.MemberNumber.Value;
            State.MemberNumberRequired = !(prefill.Locked && prefill.MemberNumber == null);
            if (!State.MemberNumberRequired)
                State.MemberNumber = 0;
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task LoginAsync() {
        await AuthService.SetReturnUrlAsync(Navigation.Uri);
        Navigation.NavigateTo("/Login");
    }

    private List<WizardStep> BuildSteps() {
        var steps = new PublicFormStepBuilder(I18n, ToastService);
        return [
            steps.Step(I18nKey.Ui.PublicFormWizard.StepPersonalData, builder => {
                builder.OpenComponent<DueSelectionPersonalDataStep>(0);
                builder.AddComponentParameter(1, nameof(DueSelectionPersonalDataStep.State), State);
                builder.AddComponentParameter(2, nameof(DueSelectionPersonalDataStep.Locked), MemberLocked);
                builder.CloseComponent();
            }, State.IsPersonalDataComplete, I18nKey.Ui.PublicFormWizard.PersonalDataRequired),
            steps.StateStep<DuesStep>(I18nKey.Ui.PublicFormWizard.StepDues, State, State.IsDuesComplete, I18nKey.Ui.PublicFormWizard.DuesRequired),
            steps.StateStep<PaymentStep>(I18nKey.Ui.PublicFormWizard.StepPayment, State, State.IsPaymentComplete, I18nKey.Ui.PublicFormWizard.PaymentRequired),
            steps.StateStep<DueSelectionOverviewStep>(I18nKey.Ui.PublicFormWizard.StepSummary, State, () => true, null)
        ];
    }

    private async Task Submit() {
        State.RecalculateDue();
        try {
            var result = await Http.PostAsJsonAsync("/api/dueselector", State.ToDTO());
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
            AppState.ResetEntryState<DueSelectorEntryState>();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }
}
