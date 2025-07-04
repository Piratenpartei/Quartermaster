﻿using System;
using LinqToDB.Mapping;
using Quartermaster.Api.DueSelector;
using Riok.Mapperly.Abstractions;

namespace Quartermaster.Data.DueSelector;

[Table("DueSelections", IsColumnAttributeRequired = false)]
public class DueSelection {
    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }

    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? Email { get; set; }
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
    public PaymentScedule PaymentSchedule { get; set; }
}

[Mapper]
public static partial class DueSelectionMapper {
    public static partial DueSelection FromDto(DueSelectionDTO dto);
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