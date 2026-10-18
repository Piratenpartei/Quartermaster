using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
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

    private DueSelectorEntryState State = default!;
    private List<WizardStep> Steps = [];
    private string? SubmittedEmail;
    private bool SubmittedDirect;

    protected override async Task OnInitializedAsync() {
        State = AppState.GetEntryState<DueSelectorEntryState>();
        await AuthService.WaitForInitializationAsync();
        var user = AuthService.CurrentUser;
        if (user != null) {
            State.FirstName = user.FirstName;
            State.LastName = user.LastName;
            State.Email = user.Email;
        }
        Steps = BuildSteps();
    }

    private List<WizardStep> BuildSteps() {
        var steps = new PublicFormStepBuilder(I18n, ToastService);
        return [
            steps.Step(I18nKey.Ui.PublicFormWizard.StepPersonalData, builder => {
                builder.OpenComponent<DueSelectionPersonalDataStep>(0);
                builder.AddComponentParameter(1, nameof(DueSelectionPersonalDataStep.State), State);
                builder.AddComponentParameter(2, nameof(DueSelectionPersonalDataStep.Locked), AuthService.IsAuthenticated);
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
