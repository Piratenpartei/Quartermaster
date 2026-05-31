namespace Quartermaster.Api.DueSelector;

public class DueSelectionDTO {
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public int MemberNumber { get; set; }

    public SelectedValuation SelectedValuation { get; set; }

    public decimal YearlyIncome { get; set; }
    public decimal MonthlyIncomeGroup { get; set; }
    public decimal ReducedAmount { get; set; } = 12;
    public decimal SelectedDue { get; set; }

    public string ReducedJustification { get; set; } = "";
    public ReducedTimeSpan ReducedTimeSpan { get; set; }

    public bool IsDirectDeposit { get; set; }
    public string AccountHolder { get; set; } = "";
    public string IBAN { get; set; } = "";
    public PaymentSchedule PaymentSchedule { get; set; } = PaymentSchedule.Annual;

    public DueSelectionDetailDTO ToDetailDto() => new() {
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
        IBAN = IBAN,
        PaymentSchedule = PaymentSchedule
    };
}

public enum SelectedValuation {
    None,
    MonthlyPayGroup,
    OnePercentYearlyPay,
    Underage,
    Reduced
}

public enum ReducedTimeSpan {
    OneYear,
    Permanent
}

public enum PaymentSchedule {
    None,
    Annual,
    Quarterly,
    Monthly
}