using System.Globalization;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.I18n;

namespace Quartermaster.Server.DueSelector;

/// <summary>
/// The limits of a requested reduction, which depend on settings and so are checked by the create endpoints rather than the
/// request validators: the wished amount lies between <see cref="DueSelectionRates.ReducedMinimum"/> and the regular minimum,
/// and a permanent reduction is only taken while the setting offers it.
/// </summary>
public static class ReducedDueRules {
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>The error for a reduction outside the limits, or null when it is fine or no reduction at all.</summary>
    public static string? Problem(DueSelectionDTO dto, decimal maximum, bool permanentAllowed) {
        if (dto.SelectedValuation != SelectedValuation.Reduced)
            return null;
        if (dto.ReducedAmount < DueSelectionRates.ReducedMinimum || dto.ReducedAmount > maximum) {
            return I18nParams.With(I18nKey.Error.Admin.DueSelection.ReducedAmountOutOfRange,
                ("min", Format(DueSelectionRates.ReducedMinimum)),
                ("max", Format(maximum)));
        }
        if (dto.ReducedTimeSpan == ReducedTimeSpan.Permanent && !permanentAllowed)
            return I18nKey.Error.Admin.DueSelection.PermanentReductionDisabled;

        return null;
    }

    /// <summary>A reduction without a classification by income records the default due, which applies if the reduction is rejected.</summary>
    public static void ApplyRejectionDefault(DueSelectionDTO dto) {
        if (dto.SelectedValuation != SelectedValuation.Reduced)
            return;
        if (dto.SelectedDue >= DueSelectionRates.LowestIncomeGroupDue)
            return;

        dto.SelectedDue = DueSelectionRates.RejectedReductionDue;
    }

    private static string Format(decimal amount) {
        return amount.ToString("#,0.##", German);
    }
}
