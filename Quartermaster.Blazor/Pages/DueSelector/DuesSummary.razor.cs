using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Pages.DueSelector;

/// <summary>The dues and payment part of the overview both public forms end with.</summary>
public partial class DuesSummary {
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public DueSelectorEntryState State { get; set; } = default!;

    private static string Amount(decimal amount) {
        return amount.ToString("C2", German);
    }

    // The summary texts carry markup, so text the applicant typed is encoded before it goes in.
    private static string Encode(string text) {
        return WebUtility.HtmlEncode(text);
    }

    private MarkupString Markup(string key, params (string Key, string Value)[] parameters) {
        return (MarkupString)I18n[I18nParams.With(key, parameters)];
    }

    private MarkupString ScheduleLine() {
        var reduced = State.SelectedValuation == SelectedValuation.Reduced;
        return State.PaymentSchedule switch {
            PaymentSchedule.Quarterly => reduced
                ? Markup(I18nKey.Ui.DueSelectorSummary.QuarterlyLineReduced)
                : Markup(I18nKey.Ui.DueSelectorSummary.QuarterlyLineWithAmount, ("amount", Amount(State.SelectedDue / 4))),
            PaymentSchedule.Monthly => reduced
                ? Markup(I18nKey.Ui.DueSelectorSummary.MonthlyLineReduced)
                : Markup(I18nKey.Ui.DueSelectorSummary.MonthlyLineWithAmount, ("amount", Amount(State.SelectedDue / 12))),
            _ => Markup(I18nKey.Ui.DueSelectorSummary.AnnualLine)
        };
    }
}
