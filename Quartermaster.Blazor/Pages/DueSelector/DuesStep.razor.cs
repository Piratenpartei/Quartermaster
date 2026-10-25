using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.DueSelector;

/// <summary>The dues step both the membership application and the due selection use.</summary>
public partial class DuesStep {
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    [Inject]
    public required I18nService I18n { get; set; }

    [Inject]
    public required ClientConfigService Config { get; set; }

    [Parameter, EditorRequired]
    public DueSelectorEntryState State { get; set; } = default!;

    private ConfirmDialog? SkipConfirm;

    // A permanent reduction may have been picked before the setting was turned off.
    protected override void OnParametersSet() {
        State.ReducedMaximum = Config.ReducedDueMaximum;
        if (!Config.PermanentReductionEnabled)
            State.ReducedTimeSpan = ReducedTimeSpan.OneYear;

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

    private async Task ConfirmSkipClassification() {
        if (SkipConfirm == null)
            return;

        var message = I18n[I18nParams.With(I18nKey.Ui.SelectReduced.SkipConfirmMessage, ("amount", FormatWholeAmount(DueSelectionRates.RejectedReductionDue)))];
        if (await SkipConfirm.ShowAsync(message))
            SetIncomeFallback(false);
    }

    private string RangeText(string key) {
        return I18nParams.With(key,
            ("min", FormatWholeAmount(DueSelectionRates.ReducedMinimum)),
            ("max", FormatWholeAmount(State.ReducedMaximum)));
    }

    private string MinimumAmountValue() {
        return DueSelectionRates.ReducedMinimum.ToString(CultureInfo.InvariantCulture);
    }

    private string MaximumAmountValue() {
        return State.ReducedMaximum.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatAmount(decimal amount) {
        return amount.ToString("C2", German);
    }

    private static string FormatWholeAmount(decimal amount) {
        return amount.ToString("#,0.## €", German);
    }

    private string TextForReducedTimeSpan(ReducedTimeSpan timeSpan) {
        return timeSpan switch {
            ReducedTimeSpan.OneYear => I18n[I18nKey.Ui.SelectReduced.TimeSpanOneYear],
            ReducedTimeSpan.Permanent => I18n[I18nKey.Ui.SelectReduced.TimeSpanPermanent],
            _ => throw new UnreachableException($"{timeSpan} is not a valid ReducedTimeSpan")
        };
    }
}
