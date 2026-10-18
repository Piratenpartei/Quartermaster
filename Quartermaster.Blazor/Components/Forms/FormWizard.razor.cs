using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components.Forms;

/// <summary>
/// Generic multi-step wizard skeleton: renders a stepper at the top, the current step's body in the
/// middle, and Back / Save Draft / Next or Submit at the bottom. Per-step validation is delegated
/// to <see cref="WizardStep.Validate"/>; auto-save on Next is delegated to <see cref="OnAdvance"/>;
/// the final submit is delegated to <see cref="OnSubmit"/>. The hosting page owns the form state,
/// the wizard owns navigation.
/// </summary>
public partial class FormWizard {
    [Inject]
    public required I18nService I18n { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

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

    private int CurrentStep;
    private bool Busy;
    private BusyKind BusyAction;
    private List<WizardStep>? _previousSteps;
    private List<FormStep> StepperSteps = [];

    protected override void OnParametersSet() {
        if (Steps != null && !ReferenceEquals(Steps, _previousSteps)) {
            _previousSteps = Steps;
            CurrentStep = ResolveInitialStep();
        }
        StepperSteps = (Steps ?? [])
            .Select(s => new FormStep(s.Title))
            .ToList();
    }

    /// <summary>First step whose <see cref="WizardStep.IsValid"/> returns false; if every step validates, the last step (typically the overview/submit step). Lets a loaded draft drop the user where they left off instead of always at step 1.</summary>
    private int ResolveInitialStep() {
        if (Steps == null || Steps.Count == 0) {
            return 0;
        }
        for (var i = 0; i < Steps.Count; i++) {
            var step = Steps[i];
            if (step.IsValid != null && !step.IsValid()) {
                return i;
            }
        }
        return Steps.Count - 1;
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
