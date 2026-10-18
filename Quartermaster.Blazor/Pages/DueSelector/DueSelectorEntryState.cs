using Quartermaster.Api.Banking;
using Quartermaster.Api.DueSelector;
using Quartermaster.Blazor.Abstract;

namespace Quartermaster.Blazor.Pages.DueSelector;

public class DueSelectorEntryState : EntryStateBase {
    public const decimal MinimumOnePercentIncome = 7200;
    public const decimal UnderageDue = 12;
    /// <summary>Below this yearly amount monthly payment is not offered.</summary>
    public const decimal MinimumMonthlyPaymentDue = 36;

    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public int MemberNumber { get; set; }

    public SelectedValuation SelectedValuation { get; set; }

    /// <summary>Yearly Income &lt; 7200€ would result in a reduced membership fee.</summary>
    public decimal YearlyIncome { get; set; } = 7200;
    public decimal MonthlyIncomeGroup { get; set; }
    public decimal ReducedAmount { get; set; } = 12;
    public decimal SelectedDue { get; set; }

    public string ReducedJustification { get; set; } = "";
    /// <summary>A reduced due can additionally be classified by monthly income; that classification becomes <see cref="SelectedDue"/>.</summary>
    public bool ReducedIncomeFallback { get; set; }
    public ReducedTimeSpan ReducedTimeSpan { get; set; }

    public bool IsDirectDeposit { get; set; }
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

        return MemberNumber != 0;
    }

    public bool IsDuesComplete() {
        return SelectedValuation switch {
            SelectedValuation.OnePercentYearlyPay => YearlyIncome >= MinimumOnePercentIncome,
            SelectedValuation.MonthlyPayGroup => true,
            SelectedValuation.Underage => true,
            SelectedValuation.Reduced => !string.IsNullOrWhiteSpace(ReducedJustification) && ReducedAmount >= 1,
            _ => false
        };
    }

    public bool IsPaymentComplete() {
        if (!IsScheduleAllowed(PaymentSchedule))
            return false;
        if (!IsDirectDeposit)
            return true;
        if (string.IsNullOrWhiteSpace(AccountHolder))
            return false;

        return Iban.IsValid(IBAN);
    }

    public bool IsScheduleAllowed(PaymentSchedule schedule) {
        if (schedule == PaymentSchedule.None)
            return false;
        if (schedule != PaymentSchedule.Monthly)
            return true;
        if (SelectedValuation == SelectedValuation.Underage)
            return false;

        return SelectedValuation != SelectedValuation.Reduced || ReducedAmount >= MinimumMonthlyPaymentDue;
    }

    /// <summary>Sets <see cref="SelectedDue"/> from the chosen valuation; a reduced due without income classification has none.</summary>
    public void RecalculateDue() {
        if (SelectedValuation == SelectedValuation.Reduced && !ReducedIncomeFallback)
            MonthlyIncomeGroup = 0;

        SelectedDue = SelectedValuation switch {
            SelectedValuation.OnePercentYearlyPay => YearlyIncome * 0.01m,
            SelectedValuation.MonthlyPayGroup => DueForIncomeGroup(MonthlyIncomeGroup),
            SelectedValuation.Underage => UnderageDue,
            SelectedValuation.Reduced => ReducedIncomeFallback ? DueForIncomeGroup(MonthlyIncomeGroup) : 0,
            _ => 0
        };
    }

    public static decimal DueForIncomeGroup(decimal monthlyIncome) {
        return monthlyIncome switch {
            >= 6000 => 600,
            >= 5000 => 480,
            >= 4000 => 360,
            >= 3000 => 240,
            >= 2500 => 180,
            >= 2000 => 120,
            _ => 72
        };
    }

    public DueSelectionDTO ToDTO() => new DueSelectionDTO {
        FirstName = FirstName,
        LastName = LastName,
        Email = Email,
        MemberNumber = MemberNumber,
        SelectedValuation = SelectedValuation,
        YearlyIncome = YearlyIncome,
        MonthlyIncomeGroup = MonthlyIncomeGroup,
        ReducedAmount = ReducedAmount,
        SelectedDue = SelectedDue,
        ReducedJustification = ReducedJustification,
        ReducedTimeSpan = ReducedTimeSpan,
        IsDirectDeposit = IsDirectDeposit,
        AccountHolder = AccountHolder,
        IBAN = IsDirectDeposit ? Iban.Normalize(IBAN) : IBAN,
        PaymentSchedule = PaymentSchedule
    };
}
