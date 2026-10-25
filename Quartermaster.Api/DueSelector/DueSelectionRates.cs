namespace Quartermaster.Api.DueSelector;

/// <summary>The fixed rates of the due selection form. Anything below them needs a reduction, which is decided separately.</summary>
public static class DueSelectionRates {
    public const decimal MinimumOnePercentIncome = 7200;
    public const decimal OnePercent = 0.01m;
    public const decimal LowestIncomeGroupDue = 72;
    public const decimal UnderageDue = 12;

    /// <summary>The least yearly amount a reduction can ask for; below it is a special case the form does not offer.</summary>
    public const decimal ReducedMinimum = 12;

    /// <summary>The yearly due recorded for a reduction without a classification, which applies if the reduction is rejected.</summary>
    public const decimal RejectedReductionDue = 120;

    /// <summary>Below this yearly amount monthly payment is not offered.</summary>
    public const decimal MinimumMonthlyPaymentDue = 36;

    /// <summary>
    /// The least <see cref="DueSelectionDTO.SelectedDue"/> a valuation allows: the due of the stated income. For a reduction it is
    /// the due if the reduction is rejected, so only its classification by income counts.
    /// </summary>
    public static decimal MinimumDue(DueSelectionDTO dto) {
        return dto.SelectedValuation switch {
            SelectedValuation.OnePercentYearlyPay => dto.YearlyIncome < MinimumOnePercentIncome ? decimal.MaxValue : dto.YearlyIncome * OnePercent,
            SelectedValuation.MonthlyPayGroup => DueForIncomeGroup(dto.MonthlyIncomeGroup),
            SelectedValuation.Underage => UnderageDue,
            SelectedValuation.Reduced => dto.MonthlyIncomeGroup > 0 ? DueForIncomeGroup(dto.MonthlyIncomeGroup) : 0,
            _ => 0
        };
    }

    /// <summary>The yearly due of a monthly net income by the income groups of the statutes.</summary>
    public static decimal DueForIncomeGroup(decimal monthlyIncome) {
        return monthlyIncome switch {
            >= 6000 => 600,
            >= 5000 => 480,
            >= 4000 => 360,
            >= 3000 => 240,
            >= 2500 => 180,
            >= 2000 => 120,
            _ => LowestIncomeGroupDue
        };
    }

    /// <summary>Monthly payment needs a yearly amount of at least <see cref="MinimumMonthlyPaymentDue"/>, so minors and small reductions pay less often.</summary>
    public static bool IsScheduleAllowed(SelectedValuation valuation, decimal reducedAmount, PaymentSchedule schedule) {
        if (schedule == PaymentSchedule.None)
            return false;
        if (schedule != PaymentSchedule.Monthly)
            return true;
        if (valuation == SelectedValuation.Underage)
            return false;

        return valuation != SelectedValuation.Reduced || reducedAmount >= MinimumMonthlyPaymentDue;
    }
}
