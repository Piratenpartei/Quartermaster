using Quartermaster.Api.Banking;
using Quartermaster.Api.DueSelector;
using Quartermaster.Blazor.Abstract;

namespace Quartermaster.Blazor.Pages.DueSelector;

public class DueSelectorEntryState : EntryStateBase {
    public const decimal MinimumOnePercentIncome = DueSelectionRates.MinimumOnePercentIncome;
    public const decimal UnderageDue = DueSelectionRates.UnderageDue;
    public const decimal DefaultMonthlyIncomeGroup = 1500;

    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public int MemberNumber { get; set; }

    /// <summary>False for a floating member: linked to the account, accepted, but without a member number yet.</summary>
    public bool MemberNumberRequired { get; set; } = true;

    public SelectedValuation SelectedValuation { get; set; }

    /// <summary>Yearly Income &lt; 7200€ would result in a reduced membership fee.</summary>
    public decimal YearlyIncome { get; set; } = 7200;
    public decimal MonthlyIncomeGroup { get; set; } = DefaultMonthlyIncomeGroup;

    /// <summary>Empty until the applicant enters the wished yearly amount of a reduction.</summary>
    public decimal? ReducedAmount { get; set; }
    public decimal SelectedDue { get; set; }

    public string ReducedJustification { get; set; } = "";
    /// <summary>
    /// A reduced due is also classified by monthly income unless the applicant skips it; the classification, or the default due
    /// when skipped, becomes <see cref="SelectedDue"/> and applies if the reduction is rejected.
    /// </summary>
    public bool ReducedIncomeFallback { get; set; } = true;
    public ReducedTimeSpan ReducedTimeSpan { get; set; }

    /// <summary>The highest wished amount of a reduction, from the server's regular minimum.</summary>
    public decimal ReducedMaximum { get; set; } = DueSelectionRates.LowestIncomeGroupDue;

    /// <summary>Null until the applicant chooses between a direct debit mandate and paying by transfer.</summary>
    public bool? IsDirectDeposit { get; set; }
    public string AccountHolder { get; set; } = "";
    public string IBAN { get; set; } = "";
    public PaymentSchedule PaymentSchedule { get; set; } = PaymentSchedule.Annual;

    public bool IsPersonalDataComplete() {
        if (string.IsNullOrWhiteSpace(FirstName))
            return false;
        if (string.IsNullOrWhiteSpace(LastName))
            return false;
        if (string.IsNullOrWhiteSpace(Email))
            return false;

        return !MemberNumberRequired || MemberNumber != 0;
    }

    public bool IsDuesComplete() {
        return SelectedValuation switch {
            SelectedValuation.OnePercentYearlyPay => YearlyIncome >= MinimumOnePercentIncome,
            SelectedValuation.MonthlyPayGroup => true,
            SelectedValuation.Underage => true,
            SelectedValuation.Reduced => !string.IsNullOrWhiteSpace(ReducedJustification) && IsReducedAmountValid(),
            _ => false
        };
    }

    public bool IsReducedAmountValid() {
        if (ReducedAmount == null)
            return false;

        return ReducedAmount >= DueSelectionRates.ReducedMinimum && ReducedAmount <= ReducedMaximum;
    }

    public bool IsPaymentComplete() {
        if (!IsScheduleAllowed(PaymentSchedule))
            return false;
        if (IsDirectDeposit == null)
            return false;
        if (IsDirectDeposit == false)
            return true;
        if (string.IsNullOrWhiteSpace(AccountHolder))
            return false;

        return Iban.IsValid(IBAN);
    }

    /// <summary>An empty wished amount counts as too small for monthly payment.</summary>
    public bool IsScheduleAllowed(PaymentSchedule schedule) {
        return DueSelectionRates.IsScheduleAllowed(SelectedValuation, ReducedAmount ?? 0, schedule);
    }

    /// <summary>Sets <see cref="SelectedDue"/> from the chosen valuation; a reduced due without income classification gets the default due.</summary>
    public void RecalculateDue() {
        SelectedDue = SelectedValuation switch {
            SelectedValuation.OnePercentYearlyPay => YearlyIncome * DueSelectionRates.OnePercent,
            SelectedValuation.MonthlyPayGroup => DueSelectionRates.DueForIncomeGroup(MonthlyIncomeGroup),
            SelectedValuation.Underage => UnderageDue,
            SelectedValuation.Reduced => ReducedIncomeFallback ? DueSelectionRates.DueForIncomeGroup(MonthlyIncomeGroup) : DueSelectionRates.RejectedReductionDue,
            _ => 0
        };
    }

    /// <summary>The slider keeps its value while hidden, so switching back finds it; only a classification in use is sent.</summary>
    private bool UsesIncomeGroup() {
        if (SelectedValuation == SelectedValuation.MonthlyPayGroup)
            return true;

        return SelectedValuation == SelectedValuation.Reduced && ReducedIncomeFallback;
    }

    public DueSelectionDTO ToDTO() => new DueSelectionDTO {
        FirstName = FirstName,
        LastName = LastName,
        Email = Email,
        MemberNumber = MemberNumber,
        SelectedValuation = SelectedValuation,
        YearlyIncome = YearlyIncome,
        MonthlyIncomeGroup = UsesIncomeGroup() ? MonthlyIncomeGroup : 0,
        ReducedAmount = ReducedAmount ?? 0,
        SelectedDue = SelectedDue,
        ReducedJustification = ReducedJustification,
        ReducedTimeSpan = ReducedTimeSpan,
        IsDirectDeposit = IsDirectDeposit == true,
        AccountHolder = AccountHolder,
        IBAN = IsDirectDeposit == true ? Iban.Normalize(IBAN) : IBAN,
        PaymentSchedule = PaymentSchedule
    };
}
