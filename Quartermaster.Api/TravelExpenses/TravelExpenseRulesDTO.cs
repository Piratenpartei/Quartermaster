using System;
using System.Collections.Generic;

namespace Quartermaster.Api.TravelExpenses;

/// <summary>One version of a chapter's travel expense rules; the version valid on a trip's start date applies.</summary>
public class TravelExpenseRulesDTO {
    public Guid Id { get; set; }

    /// <summary>Null for the global default, the federal rules every chapter without its own falls back to.</summary>
    public Guid? ChapterId { get; set; }

    public DateOnly ValidFrom { get; set; }

    /// <summary>Printed in the form title, e.g. "J2.0".</summary>
    public string VersionLabel { get; set; } = "";

    /// <summary>Lines above the form: organisation, address, treasurer contact. One line per entry.</summary>
    public string HeaderLines { get; set; } = "";

    /// <summary>The rules text printed on the page after the form; empty leaves that page out.</summary>
    public string RulesText { get; set; } = "";

    public string TemplateKey { get; set; } = "";

    public List<RateStepDTO> CarRates { get; set; } = [];

    /// <summary>Empty when the rules don't reimburse motorcycles.</summary>
    public List<RateStepDTO> MotorcycleRates { get; set; } = [];

    public decimal MealDayTripRate { get; set; }
    public decimal MealArrivalDepartureRate { get; set; }
    public decimal MealFullDayRate { get; set; }
    public decimal BreakfastReductionPercent { get; set; }
    public decimal LunchReductionPercent { get; set; }
    public decimal DinnerReductionPercent { get; set; }
    public decimal AccommodationFlatRate { get; set; }

    /// <summary>Days after the trip within which the application is due; later ones only get a note.</summary>
    public int SubmissionDeadlineDays { get; set; }

    /// <summary>Day and month in the year after the trip from which a reimbursement needs a board decision.</summary>
    public int CutoffDay { get; set; }
    public int CutoffMonth { get; set; }
}

/// <summary>A per-km rate up to a total distance; the last step has no limit.</summary>
public class RateStepDTO {
    public decimal? UpToKm { get; set; }
    public decimal RatePerKm { get; set; }
}
