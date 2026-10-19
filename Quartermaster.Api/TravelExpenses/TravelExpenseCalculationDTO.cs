using System;
using System.Collections.Generic;

namespace Quartermaster.Api.TravelExpenses;

public enum MealDayKind {
    None,
    DayTrip,
    ArrivalDeparture,
    FullDay
}

public enum TravelExpenseNoteSeverity {
    Info,
    Warning,
    Problem
}

public class TravelExpenseCalculationDTO {
    public List<MealDayDTO> MealDays { get; set; } = [];
    public int ArrivalDepartureDays { get; set; }
    public int FullDays { get; set; }
    public decimal MealAllowanceGross { get; set; }
    public decimal MealReductions { get; set; }
    public decimal MealAllowance { get; set; }

    public decimal CarKm { get; set; }
    public decimal CarAmount { get; set; }
    public decimal MotorcycleKm { get; set; }
    public decimal MotorcycleAmount { get; set; }
    public decimal TicketsAmount { get; set; }

    /// <summary>Hotel invoices after breakfast deductions.</summary>
    public decimal AccommodationReceiptAmount { get; set; }
    public int AccommodationFlatNights { get; set; }
    public decimal AccommodationFlatAmount { get; set; }

    public List<OtherCostLineDTO> OtherCosts { get; set; } = [];
    public decimal OtherCostsAmount { get; set; }

    public decimal TravelAmount { get; set; }
    public decimal AccommodationAmount { get; set; }
    public decimal Total { get; set; }
    public decimal Donation { get; set; }
    public decimal Payout { get; set; }

    /// <summary>The amount each module contributes, keyed by module id; provided meals carry their (negative) reduction.</summary>
    public Dictionary<Guid, decimal> ModuleAmounts { get; set; } = [];

    public List<TravelExpenseNoteDTO> Notes { get; set; } = [];
}

public class MealDayDTO {
    public DateOnly Date { get; set; }
    public MealDayKind Kind { get; set; }
    public decimal Allowance { get; set; }
    public decimal Reduction { get; set; }
    public decimal Net { get; set; }
}

public class OtherCostLineDTO {
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
}

public class TravelExpenseNoteDTO {
    public TravelExpenseNoteSeverity Severity { get; set; }

    /// <summary>An i18n key, possibly carrying parameters via <c>I18nParams.With</c>.</summary>
    public string MessageKey { get; set; } = "";

    /// <summary>The module the note is about, for a link into the trip builder.</summary>
    public Guid? ModuleId { get; set; }
}
