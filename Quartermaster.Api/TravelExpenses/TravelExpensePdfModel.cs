using System.Collections.Generic;

namespace Quartermaster.Api.TravelExpenses;

/// <summary>What the travel expense template sees as <c>form</c>: every value already formatted for print.</summary>
public class TravelExpensePdfModel {
    public string VersionLabel { get; set; } = "";
    public string ValidFrom { get; set; } = "";
    public List<string> HeaderLines { get; set; } = [];

    public string Name { get; set; } = "";
    public string MemberNumber { get; set; } = "";
    public string Street { get; set; } = "";
    public string PostCodeCity { get; set; } = "";
    public string Email { get; set; } = "";
    public string Function { get; set; } = "";
    public string MembershipChapter { get; set; } = "";
    public string TravelChapter { get; set; } = "";

    public string Purpose { get; set; } = "";
    public string ResolutionNumber { get; set; } = "";
    public string ResolutionDate { get; set; } = "";
    public string Destination { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string StartTime { get; set; } = "";
    public string EndDate { get; set; } = "";
    public string EndTime { get; set; } = "";

    public string CarRate { get; set; } = "";
    public string MotorcycleRate { get; set; } = "";
    public string Tickets { get; set; } = "";
    public string MileageKm { get; set; } = "";
    public string MileageRate { get; set; } = "";
    public string Mileage { get; set; } = "";

    public string DayTripRate { get; set; } = "";
    public string ArrivalDepartureRate { get; set; } = "";
    public string FullDayRate { get; set; } = "";
    public int ArrivalDepartureDays { get; set; }
    public string ArrivalDepartureAmount { get; set; } = "";
    public int FullDays { get; set; }
    public string FullDaysAmount { get; set; } = "";
    public string MealReductions { get; set; } = "";
    public string MealAllowance { get; set; } = "";

    public string AccommodationReceipt { get; set; } = "";
    public string AccommodationFlatRate { get; set; } = "";
    public int AccommodationFlatNights { get; set; }
    public string AccommodationFlat { get; set; } = "";

    /// <summary>At most the three lines the official form has; more costs are summed up with a reference to the appendix.</summary>
    public List<TravelExpensePdfLine> OtherCosts { get; set; } = [];

    public string Total { get; set; } = "";
    public string Donation { get; set; } = "";
    public string Payout { get; set; } = "";
    public string Iban { get; set; } = "";
    public string Bic { get; set; } = "";
    public int AttachmentCount { get; set; }
    public string FormDate { get; set; } = "";

    public List<string> RulesParagraphs { get; set; } = [];
    public List<TravelExpensePdfTimelineRow> Timeline { get; set; } = [];
    public List<TravelExpensePdfMealDay> MealDays { get; set; } = [];
    public List<TravelExpensePdfLine> AllOtherCosts { get; set; } = [];
    public List<TravelExpensePdfLine> Notes { get; set; } = [];
}

public class TravelExpensePdfLine {
    public string Description { get; set; } = "";
    public string Amount { get; set; } = "";
}

public class TravelExpensePdfTimelineRow {
    public string When { get; set; } = "";
    public string Title { get; set; } = "";
    public string Detail { get; set; } = "";
    public string Amount { get; set; } = "";
}

public class TravelExpensePdfMealDay {
    public string Date { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Allowance { get; set; } = "";
    public string Reduction { get; set; } = "";
    public string Net { get; set; } = "";
}
