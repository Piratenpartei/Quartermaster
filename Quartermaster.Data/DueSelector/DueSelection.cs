using System;
using LinqToDB.Mapping;
using Quartermaster.Api.DueSelector;

namespace Quartermaster.Data.DueSelector;

[Table(TableName, IsColumnAttributeRequired = false)]
public class DueSelection {
    public const string TableName = "DueSelections";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }

    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? EMail { get; set; }
    public int? MemberNumber { get; set; }

    public SelectedValuation SelectedValuation { get; set; }

    public decimal YearlyIncome { get; set; }
    public decimal MonthlyIncomeGroup { get; set; }
    public decimal ReducedAmount { get; set; }
    public decimal SelectedDue { get; set; }

    public string ReducedJustification { get; set; } = "";
    public ReducedTimeSpan ReducedTimeSpan { get; set; }

    public bool IsDirectDeposit { get; set; }
    public string AccountHolder { get; set; } = "";
    public string IBAN { get; set; } = "";
    public PaymentSchedule PaymentSchedule { get; set; }

    // Processing
    public DueSelectionStatus Status { get; set; }
    public Guid? ProcessedByUserId { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime? AnonymizedAt { get; set; }
}

