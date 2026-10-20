namespace Quartermaster.Api.DueSelector;

/// <summary>The fixed rates of the due selection form. Anything below them needs a reduction, which is decided separately.</summary>
public static class DueSelectionRates {
    public const decimal MinimumOnePercentIncome = 7200;
    public const decimal OnePercent = 0.01m;
    public const decimal LowestIncomeGroupDue = 72;
    public const decimal UnderageDue = 12;

    /// <summary>The least <see cref="DueSelectionDTO.SelectedDue"/> a valuation allows; 0 for a reduction, whose amount is decided on.</summary>
    public static decimal MinimumDue(DueSelectionDTO dto) {
        return dto.SelectedValuation switch {
            SelectedValuation.OnePercentYearlyPay => dto.YearlyIncome < MinimumOnePercentIncome ? decimal.MaxValue : dto.YearlyIncome * OnePercent,
            SelectedValuation.MonthlyPayGroup => LowestIncomeGroupDue,
            SelectedValuation.Underage => UnderageDue,
            _ => 0
        };
    }
}
