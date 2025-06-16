using Quartermaster.Api.DueSelector;
using Quartermaster.Blazor.Abstract;
using Riok.Mapperly.Abstractions;

namespace Quartermaster.Blazor.Pages.DueSelector;

public class DueSelectorEntryState : EntryStateBase {
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public int MemberNumber { get; set; }

    public SelectedValuation SelectedValuation { get; set; }

    // Yearly Income < 7200€ would result in a reduced membership fee
    public decimal YearlyIncome { get; set; } = 7200;
    public decimal MonthlyIncomeGroup { get; set; }
    public decimal ReducedAmount { get; set; } = 12;
    public decimal SelectedDue { get; set; }

    public string ReducedJustification { get; set; } = "";
    public ReducedTimeSpan ReducedTimeSpan { get; set; }

    public bool IsDirectDeposit { get; set; }
    public string AccountHolder { get; set; } = "";
    public string IBAN { get; set; } = "";
    public PaymentScedule PaymentScedule { get; set; } = PaymentScedule.Annual;

    public DueSelectionDTO ToDTO() => DueSelectorMapper.ToApiDTO(this);
}

[Mapper]
public static partial class DueSelectorMapper {
    public static partial DueSelectionDTO ToApiDTO(DueSelectorEntryState entryState);
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