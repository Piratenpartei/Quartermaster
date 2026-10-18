using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Pages.DueSelector;

/// <summary>The dues step both the membership application and the due selection use.</summary>
public partial class DuesStep {
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public DueSelectorEntryState State { get; set; } = default!;

    protected override void OnParametersSet() {
        State.RecalculateDue();
    }

    private bool IsSelected(SelectedValuation valuation) {
        return State.SelectedValuation == valuation;
    }

    private void Select(SelectedValuation valuation) {
        State.SelectedValuation = valuation;
        State.RecalculateDue();
    }

    private void SetIncomeFallback(bool fallback) {
        State.ReducedIncomeFallback = fallback;
        State.RecalculateDue();
    }

    private static string FormatAmount(decimal amount) {
        return amount.ToString("C2", German);
    }

    private string TextForReducedTimeSpan(ReducedTimeSpan timeSpan) {
        return timeSpan switch {
            ReducedTimeSpan.OneYear => I18n[I18nKey.Ui.SelectReduced.TimeSpanOneYear],
            ReducedTimeSpan.Permanent => I18n[I18nKey.Ui.SelectReduced.TimeSpanPermanent],
            _ => throw new UnreachableException($"{timeSpan} is not a valid ReducedTimeSpan")
        };
    }
}
