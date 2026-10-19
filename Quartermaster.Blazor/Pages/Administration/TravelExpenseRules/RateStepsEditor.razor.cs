using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.TravelExpenses;

namespace Quartermaster.Blazor.Pages.Administration.TravelExpenseRules;

/// <summary>Mileage steps; the last one is always open-ended, so adding a step closes the previous last one.</summary>
public partial class RateStepsEditor {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public List<RateStepDTO> Steps { get; set; } = default!;

    [Parameter]
    public bool AllowEmpty { get; set; }

    [Parameter]
    public string EmptyText { get; set; } = "";

    private void Add() {
        if (Steps.Count == 0) {
            Steps.Add(new RateStepDTO { RatePerKm = 0.30m });
            return;
        }

        var last = Steps[^1];
        var previousLimit = Steps.Count > 1 ? Steps[^2].UpToKm ?? 0 : 0;
        last.UpToKm = previousLimit + 100;
        Steps.Add(new RateStepDTO { RatePerKm = last.RatePerKm });
    }

    private void Remove(RateStepDTO step) {
        Steps.Remove(step);
        if (Steps.Count > 0)
            Steps[^1].UpToKm = null;
    }
}
