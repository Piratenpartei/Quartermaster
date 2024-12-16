using Quartermaster.Blazor.Abstract;

namespace Quartermaster.Blazor.Pages.DueSelector;

public class DueSelectorEntryState : EntryStateBase {
    public string Name { get; set; } = "";
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
    public PaymentScedule PaymentScedule { get; set; }
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

public enum PaymentScedule {
    None,
    Annual,
    Quarterly,
    Monthly
}