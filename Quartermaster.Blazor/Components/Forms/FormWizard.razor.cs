using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components.Forms;

/// <summary>
/// Generic multi-step wizard skeleton: renders a stepper at the top, the current step's body in the
/// middle, and Back / Save Draft / Next or Submit at the bottom. Per-step validation is delegated
/// to <see cref="WizardStep.Validate"/>; auto-save on Next is delegated to <see cref="OnAdvance"/>;
/// the final submit is delegated to <see cref="OnSubmit"/>. The hosting page owns the form state,
/// the wizard owns navigation. The current step is mirrored as <c>?step=N</c> (1-based), so browser back and forward
/// move between steps and a reload returns to the step as long as every step before it is valid.
/// </summary>
public partial class FormWizard : IDisposable {
    private const string StepQueryParameter = "step";

    [Inject]
    public required I18nService I18n { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }
    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Parameter]
    public required List<WizardStep> Steps { get; set; }

    [Parameter]
    public bool ShowSaveDraftButton { get; set; }

    [Parameter]
    public bool CanSubmit { get; set; } = true;

    [Parameter]
    public string? SubmitLabel { get; set; }

    /// <summary>Invoked when the user clicks Next on a step that validated OK; receives the index of the step the user is leaving. The page should auto-save here.</summary>
    [Parameter]
    public EventCallback<int> OnAdvance { get; set; }

    [Parameter]
    public EventCallback OnSubmit { get; set; }

    [Parameter]
    public EventCallback OnSaveDraft { get; set; }

    /// <summary>
    /// Step to reopen a draft at when the URL names none. Without it the wizard opens at the first incomplete step, which
    /// skips past steps that only hold optional fields.
    /// </summary>
    [Parameter]
    public int? ResumeStep { get; set; }

    private int CurrentStep;
    private bool Busy;
    private BusyKind BusyAction;
    private List<WizardStep>? _previousSteps;
    private List<FormStep> StepperSteps = [];

    protected override void OnInitialized() {
        Navigation.LocationChanged += OnLocationChanged;
    }

    public void Dispose() {
        Navigation.LocationChanged -= OnLocationChanged;
    }

    protected override void OnParametersSet() {
        if (Steps != null && !ReferenceEquals(Steps, _previousSteps)) {
            _previousSteps = Steps;
            var requested = StepFromUrl();
            CurrentStep = requested != null && IsStepReachable(requested.Value) ? requested.Value : ResolveInitialStep();
            SyncUrl(replace: true);
        }
        StepperSteps = (Steps ?? [])
            .Select(s => new FormStep(s.Title))
            .ToList();
    }

    /// <summary>First step whose <see cref="WizardStep.IsValid"/> returns false, up to <see cref="ResumeStep"/> or else the last step (typically the overview/submit step). Lets a loaded draft drop the user where they left off instead of always at step 1.</summary>
    private int ResolveInitialStep() {
        if (Steps == null || Steps.Count == 0) {
            return 0;
        }
        var last = ResumeStep == null ? Steps.Count - 1 : Math.Clamp(ResumeStep.Value, 0, Steps.Count - 1);
        for (var i = 0; i < last; i++) {
            var step = Steps[i];
            if (step.IsValid != null && !step.IsValid()) {
                return i;
            }
        }
        return last;
    }

    private bool IsLastStep => Steps != null && CurrentStep == Steps.Count - 1;

    private bool IsStepReachable(int idx) {
        if (Steps == null || idx <= 0) {
            return true;
        }
        for (var i = 0; i < idx; i++) {
            var step = Steps[i];
            if (step.IsValid != null && !step.IsValid()) {
                return false;
            }
        }
        return true;
    }

    private async Task GoToStep(int idx) {
        if (Steps == null || idx == CurrentStep || idx < 0 || idx >= Steps.Count) {
            return;
        }
        if (!IsStepReachable(idx)) {
            return;
        }
        Busy = true;
        BusyAction = BusyKind.Jump;
        StateHasChanged();
        try {
            if (OnAdvance.HasDelegate) {
                await OnAdvance.InvokeAsync(CurrentStep);
            }
            CurrentStep = idx;
            SyncUrl(replace: false);
        } catch (Exception ex) {
            ToastService.Error(ex.Message);
        } finally {
            Busy = false;
            StateHasChanged();
        }
    }

    private async Task GoNext() {
        var step = Steps[CurrentStep];
        if (step.Validate != null && !await step.Validate.Invoke()) {
            return;
        }

        Busy = true;
        BusyAction = BusyKind.Next;
        StateHasChanged();
        try {
            if (OnAdvance.HasDelegate) {
                await OnAdvance.InvokeAsync(CurrentStep);
            }
            if (CurrentStep < Steps.Count - 1) {
                CurrentStep++;
                SyncUrl(replace: false);
            }
        } catch (Exception ex) {
            ToastService.Error(ex.Message);
        } finally {
            Busy = false;
            StateHasChanged();
        }
    }

    private void GoBack() {
        if (CurrentStep > 0) {
            CurrentStep--;
            SyncUrl(replace: false);
        }
    }

    private async Task HandleSubmit() {
        var step = Steps[CurrentStep];
        if (step.Validate != null && !await step.Validate.Invoke()) {
            return;
        }
        Busy = true;
        BusyAction = BusyKind.Submit;
        StateHasChanged();
        try {
            if (OnSubmit.HasDelegate) {
                await OnSubmit.InvokeAsync();
            }
        } catch (Exception ex) {
            ToastService.Error(ex.Message);
        } finally {
            Busy = false;
            StateHasChanged();
        }
    }

    private async Task HandleSaveDraft() {
        Busy = true;
        BusyAction = BusyKind.SaveDraft;
        StateHasChanged();
        try {
            if (OnSaveDraft.HasDelegate) {
                await OnSaveDraft.InvokeAsync();
            }
        } catch (Exception ex) {
            ToastService.Error(ex.Message);
        } finally {
            Busy = false;
            StateHasChanged();
        }
    }

    /// <summary>The 0-based step named in the URL, or null when it names none of the steps.</summary>
    private int? StepFromUrl() {
        var raw = HttpUtility.ParseQueryString(Navigation.ToAbsoluteUri(Navigation.Uri).Query)[StepQueryParameter];
        if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var oneBased))
            return null;
        if (Steps == null || oneBased < 1 || oneBased > Steps.Count)
            return null;

        return oneBased - 1;
    }

    private void SyncUrl(bool replace) {
        if (StepFromUrl() == CurrentStep)
            return;

        Navigation.NavigateTo(Navigation.GetUriWithQueryParameter(StepQueryParameter, CurrentStep + 1), new NavigationOptions { ReplaceHistoryEntry = replace });
    }

    // Browser back and forward only change the query, so the page stays and the wizard follows the URL. A step that
    // isn't reachable yet (e.g. typed into the URL) is refused by putting the current step back.
    private async void OnLocationChanged(object? sender, LocationChangedEventArgs e) {
        var requested = StepFromUrl();
        if (requested == null || requested == CurrentStep || Busy)
            return;

        await InvokeAsync(async () => {
            if (!IsStepReachable(requested.Value)) {
                SyncUrl(replace: true);
                return;
            }

            try {
                if (OnAdvance.HasDelegate) {
                    await OnAdvance.InvokeAsync(CurrentStep);
                }
                CurrentStep = requested.Value;
            } catch (Exception ex) {
                ToastService.Error(ex.Message);
                SyncUrl(replace: true);
            }
            StateHasChanged();
        });
    }

    private enum BusyKind { None, Next, Submit, SaveDraft, Jump }
}

public class WizardStep {
    public required string Title { get; set; }
    public required RenderFragment Content { get; set; }
    /// <summary>Runs when the user clicks Next on this step; may surface toasts/errors. Returning false blocks the advance.</summary>
    public Func<Task<bool>>? Validate { get; set; }
    /// <summary>Side-effect-free predicate the wizard polls on every render to decide whether stepper buttons for downstream steps are clickable. Returning false makes everything after this step un-jumpable until the data is fixed.</summary>
    public Func<bool>? IsValid { get; set; }
}
