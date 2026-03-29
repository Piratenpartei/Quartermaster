using System;

namespace Quartermaster.Api.DueSelector;

public class DueSelectionDetailDTO {
    public Guid Id { get; set; }

    // Person
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? EMail { get; set; }
    public int? MemberNumber { get; set; }

    // Income & Dues
    public int SelectedValuation { get; set; }
    public decimal YearlyIncome { get; set; }
    public decimal MonthlyIncomeGroup { get; set; }
    public decimal ReducedAmount { get; set; }
    public decimal SelectedDue { get; set; }
    public string ReducedJustification { get; set; } = "";
    public int ReducedTimeSpan { get; set; }

    // Payment
    public bool IsDirectDeposit { get; set; }
    public string AccountHolder { get; set; } = "";
    public string IBAN { get; set; } = "";
    public int PaymentSchedule { get; set; }

    // Processing
    public int Status { get; set; }
    public DateTime? ProcessedAt { get; set; }

    // Linked motion
    public Guid? LinkedMotionId { get; set; }
}
