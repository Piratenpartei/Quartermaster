using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Components;

namespace Quartermaster.Blazor.Components.Forms;

/// <summary>
/// The step breadcrumb shared by <see cref="FormWizard"/> and the page-per-step public forms. A step is clickable when it
/// is reachable and not the current one; it then links to <see cref="FormStep.Href"/> or raises <see cref="OnStepClick"/>.
/// </summary>
public partial class FormStepper {
    [Parameter, EditorRequired]
    public IReadOnlyList<FormStep> Steps { get; set; } = [];

    [Parameter]
    public int CurrentStep { get; set; }

    /// <summary>Defaults to the steps before the current one.</summary>
    [Parameter]
    public Func<int, bool>? IsReachable { get; set; }

    [Parameter]
    public EventCallback<int> OnStepClick { get; set; }

    [Parameter]
    public bool Busy { get; set; }

    private bool IsClickable(int idx) {
        if (idx == CurrentStep)
            return false;

        return IsReachable == null ? idx < CurrentStep : IsReachable(idx);
    }
}

public record FormStep(string Title, string? Href = null);
