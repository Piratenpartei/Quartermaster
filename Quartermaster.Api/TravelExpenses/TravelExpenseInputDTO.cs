using System;
using System.Collections.Generic;

namespace Quartermaster.Api.TravelExpenses;

public enum TripModuleKind {
    VehicleLeg,
    TransitLeg,
    HotelStay,
    ProvidedMeal,
    OtherCost
}

public enum TripVehicle {
    Car,
    Motorcycle
}

public enum TripTransitMode {
    Train,
    Bus,
    Plane,
    Other
}

public enum TripMeal {
    Breakfast,
    Lunch,
    Dinner
}

public enum TravelDonationMode {
    None,
    All,
    Amount,
    Parts
}

/// <summary>Everything the travel expense form collects; the calculator and the PDF work from this alone.</summary>
public class TravelExpenseInputDTO {
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public int? MemberNumber { get; set; }
    public string Email { get; set; } = "";
    public string Street { get; set; } = "";
    public string PostCode { get; set; } = "";
    public string City { get; set; } = "";
    public Guid? MembershipChapterId { get; set; }
    public string MembershipChapterName { get; set; } = "";

    /// <summary>The chapter the trip was made for; its rules apply.</summary>
    public Guid? TravelChapterId { get; set; }
    public string TravelChapterName { get; set; } = "";
    public string Function { get; set; } = "";

    public string Purpose { get; set; } = "";
    public string Destination { get; set; } = "";
    public string ResolutionNumber { get; set; } = "";
    public DateTime? ResolutionDate { get; set; }

    public TripPointDTO Start { get; set; } = new();
    public TripPointDTO End { get; set; } = new();
    public List<TripModuleDTO> Modules { get; set; } = [];

    public TravelDonationMode DonationMode { get; set; }
    public decimal DonationAmount { get; set; }
    public bool DonateTravel { get; set; }
    public bool DonateMealAllowance { get; set; }
    public bool DonateAccommodation { get; set; }
    public bool DonateOther { get; set; }

    public string Iban { get; set; } = "";
    public string Bic { get; set; } = "";

    /// <summary>Attachments handed in on paper; receipts from the cache are counted on top.</summary>
    public int PaperAttachmentCount { get; set; }

    public DateTime FormDate { get; set; } = DateTime.Today;

    /// <summary>The furthest wizard step reached with Next, so a reopened draft continues there.</summary>
    public int ResumeStep { get; set; }
}

/// <summary>Where and when the absence from home starts or ends, in local time.</summary>
public class TripPointDTO {
    public string Place { get; set; } = "";
    public DateTime? At { get; set; }
}

/// <summary>One element of the trip timeline. <see cref="Kind"/> decides which fields count.</summary>
public class TripModuleDTO {
    public Guid Id { get; set; } = Guid.NewGuid();
    public TripModuleKind Kind { get; set; }

    /// <summary>Departure, check-in, the day of a provided meal or the date of another cost.</summary>
    public DateTime? At { get; set; }

    /// <summary>Arrival of a leg or check-out of a hotel stay.</summary>
    public DateTime? Until { get; set; }

    public TripVehicle Vehicle { get; set; }
    public TripTransitMode TransitMode { get; set; }
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public decimal Km { get; set; }

    /// <summary>Ticket price, hotel invoice total or the other cost.</summary>
    public decimal Amount { get; set; }

    public bool HotelByReceipt { get; set; } = true;
    public bool BreakfastIncluded { get; set; }

    /// <summary>Breakfast per night as itemised on the invoice; empty means not itemised.</summary>
    public decimal? BreakfastAmountPerNight { get; set; }

    public TripMeal Meal { get; set; }
    public string Description { get; set; } = "";
    public List<Guid> ReceiptIds { get; set; } = [];
}
