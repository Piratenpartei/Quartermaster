using System;
using System.Collections.Generic;
using Quartermaster.Api.I18n;
using Quartermaster.Api.TravelExpenses;
using Quartermaster.Server.Forms.PdfTemplating;

namespace Quartermaster.Server.TravelExpenses;

/// <summary>A two-day trip with car, train, hotel and a provided dinner for the template editor's preview.</summary>
public class TravelExpenseMockData : IFormMockData {
    private readonly I18nService _i18n;

    public TravelExpenseMockData(I18nService i18n) {
        _i18n = i18n;
    }

    public string FormKey => TravelExpensePdfService.FormKey;

    public IDictionary<string, object> BuildModel() {
        var input = new TravelExpenseInputDTO {
            FirstName = "Max",
            LastName = "Mustermann",
            MemberNumber = 12345,
            Email = "max@example.org",
            Street = "Beispielstraße 1",
            PostCode = "30159",
            City = "Hannover",
            MembershipChapterName = "Niedersachsen",
            TravelChapterName = "Piratenpartei Deutschland",
            Function = "Schatzmeister",
            Purpose = "Bundesparteitag",
            Destination = "Berlin",
            ResolutionNumber = "BuVo-2026-042",
            ResolutionDate = new DateTime(2026, 9, 1),
            Start = new TripPointDTO { Place = "Hannover", At = new DateTime(2026, 11, 27, 14, 0, 0) },
            End = new TripPointDTO { Place = "Hannover", At = new DateTime(2026, 11, 28, 21, 30, 0) },
            Modules = [
                new TripModuleDTO { Kind = TripModuleKind.VehicleLeg, Vehicle = TripVehicle.Car, From = "Hannover", To = "Hannover Hbf", Km = 8, At = new DateTime(2026, 11, 27, 14, 0, 0) },
                new TripModuleDTO { Kind = TripModuleKind.TransitLeg, TransitMode = TripTransitMode.Train, From = "Hannover Hbf", To = "Berlin Hbf", Amount = 39.90m, At = new DateTime(2026, 11, 27, 14, 31, 0), Until = new DateTime(2026, 11, 27, 16, 10, 0) },
                new TripModuleDTO { Kind = TripModuleKind.HotelStay, Description = "Hotel am Hauptbahnhof", Amount = 89m, BreakfastIncluded = true, BreakfastAmountPerNight = 12m, At = new DateTime(2026, 11, 27, 17, 0, 0), Until = new DateTime(2026, 11, 28, 9, 0, 0) },
                new TripModuleDTO { Kind = TripModuleKind.ProvidedMeal, Meal = TripMeal.Dinner, At = new DateTime(2026, 11, 27) },
                new TripModuleDTO { Kind = TripModuleKind.TransitLeg, TransitMode = TripTransitMode.Train, From = "Berlin Hbf", To = "Hannover Hbf", Amount = 39.90m, At = new DateTime(2026, 11, 28, 18, 46, 0), Until = new DateTime(2026, 11, 28, 20, 25, 0) },
                new TripModuleDTO { Kind = TripModuleKind.OtherCost, Description = "Parken Hannover Hbf", Amount = 18m, At = new DateTime(2026, 11, 28) }
            ],
            DonationMode = TravelDonationMode.Amount,
            DonationAmount = 50,
            Iban = "DE89370400440532013000",
            FormDate = new DateTime(2026, 12, 1)
        };
        var rules = TravelExpenseRulePresets.Federal();
        var calc = TravelExpenseCalculator.Calculate(input, rules, new DateOnly(2026, 12, 1));
        return new Dictionary<string, object> {
            ["form"] = TravelExpensePdfModelBuilder.Build(input, rules, calc, 3, _i18n)
        };
    }
}
