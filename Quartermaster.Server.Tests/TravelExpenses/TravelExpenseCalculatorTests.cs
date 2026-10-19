using System;
using System.Linq;
using System.Threading.Tasks;
using Quartermaster.Api.I18n;
using Quartermaster.Api.TravelExpenses;

namespace Quartermaster.Server.Tests.TravelExpenses;

public class TravelExpenseCalculatorTests {
    private static readonly DateOnly Today = new(2026, 10, 20);

    private static TravelExpenseInputDTO Trip(DateTime start, DateTime end, params TripModuleDTO[] modules) {
        return new TravelExpenseInputDTO {
            FirstName = "Anna",
            LastName = "Pirat",
            MemberNumber = 1,
            Street = "Teststraße 1",
            PostCode = "30159",
            City = "Hannover",
            Purpose = "Landesparteitag",
            ResolutionNumber = "B-1",
            Iban = "DE89370400440532013000",
            Start = new TripPointDTO { Place = "Hannover", At = start },
            End = new TripPointDTO { Place = "Hannover", At = end },
            Modules = modules.ToList()
        };
    }

    private static TravelExpenseCalculationDTO Calculate(TravelExpenseInputDTO input, TravelExpenseRulesDTO? rules = null, DateOnly? today = null) {
        return TravelExpenseCalculator.Calculate(input, rules ?? TravelExpenseRulePresets.Federal(), today ?? Today);
    }

    private static TripModuleDTO Meal(DateTime day, TripMeal meal) {
        return new TripModuleDTO { Kind = TripModuleKind.ProvidedMeal, At = day, Meal = meal };
    }

    private static TripModuleDTO Car(decimal km) {
        return new TripModuleDTO { Kind = TripModuleKind.VehicleLeg, Vehicle = TripVehicle.Car, Km = km, At = new DateTime(2026, 10, 1, 8, 0, 0) };
    }

    private static TripModuleDTO Hotel(DateTime checkIn, DateTime checkOut, decimal amount) {
        return new TripModuleDTO { Kind = TripModuleKind.HotelStay, At = checkIn, Until = checkOut, Amount = amount };
    }

    [Test]
    public async Task Day_trip_over_eight_hours_gets_the_partial_rate() {
        var result = Calculate(Trip(new DateTime(2026, 10, 1, 8, 0, 0), new DateTime(2026, 10, 1, 17, 0, 0)));

        await Assert.That(result.MealAllowance).IsEqualTo(14m);
        await Assert.That(result.MealDays.Single().Kind).IsEqualTo(MealDayKind.DayTrip);
    }

    [Test]
    public async Task Day_trip_of_exactly_eight_hours_gets_nothing() {
        var result = Calculate(Trip(new DateTime(2026, 10, 1, 8, 0, 0), new DateTime(2026, 10, 1, 16, 0, 0)));

        await Assert.That(result.MealAllowance).IsEqualTo(0m);
    }

    [Test]
    public async Task Three_day_trip_gets_arrival_full_and_departure_day() {
        var result = Calculate(Trip(new DateTime(2026, 10, 1, 8, 0, 0), new DateTime(2026, 10, 3, 20, 0, 0),
            Hotel(new DateTime(2026, 10, 1, 18, 0, 0), new DateTime(2026, 10, 3, 9, 0, 0), 150)));

        await Assert.That(result.MealAllowance).IsEqualTo(56m);
        await Assert.That(result.ArrivalDepartureDays).IsEqualTo(2);
        await Assert.That(result.FullDays).IsEqualTo(1);
    }

    [Test]
    public async Task Night_trip_without_hotel_counts_on_the_day_with_most_hours() {
        var evening = Calculate(Trip(new DateTime(2026, 10, 1, 14, 0, 0), new DateTime(2026, 10, 2, 2, 0, 0)));
        var morning = Calculate(Trip(new DateTime(2026, 10, 1, 21, 0, 0), new DateTime(2026, 10, 2, 9, 0, 0)));

        await Assert.That(evening.MealAllowance).IsEqualTo(14m);
        await Assert.That(evening.MealDays.Single(d => d.Kind == MealDayKind.DayTrip).Date).IsEqualTo(new DateOnly(2026, 10, 1));
        await Assert.That(morning.MealDays.Single(d => d.Kind == MealDayKind.DayTrip).Date).IsEqualTo(new DateOnly(2026, 10, 2));
    }

    [Test]
    public async Task Provided_lunch_on_a_full_day_reduces_by_forty_percent_of_the_full_day_rate() {
        var result = Calculate(Trip(new DateTime(2026, 10, 1, 8, 0, 0), new DateTime(2026, 10, 3, 20, 0, 0),
            Meal(new DateTime(2026, 10, 2), TripMeal.Lunch)));

        await Assert.That(result.MealReductions).IsEqualTo(11.20m);
        await Assert.That(result.MealAllowance).IsEqualTo(44.80m);
    }

    [Test]
    public async Task Reductions_never_exceed_the_days_allowance() {
        var day = new DateTime(2026, 10, 1);
        var result = Calculate(Trip(new DateTime(2026, 10, 1, 8, 0, 0), new DateTime(2026, 10, 2, 20, 0, 0),
            Meal(day, TripMeal.Breakfast), Meal(day, TripMeal.Lunch), Meal(day, TripMeal.Dinner)));

        var first = result.MealDays.First();
        await Assert.That(first.Reduction).IsEqualTo(14m);
        await Assert.That(first.Net).IsEqualTo(0m);
        await Assert.That(result.MealAllowance).IsEqualTo(14m);
    }

    [Test]
    public async Task Federal_car_rate_is_thirty_cents() {
        var result = Calculate(Trip(new DateTime(2026, 10, 1, 8, 0, 0), new DateTime(2026, 10, 1, 12, 0, 0), Car(123)));

        await Assert.That(result.CarAmount).IsEqualTo(36.90m);
    }

    [Test]
    public async Task Baden_wuerttemberg_rate_steps_apply_to_the_total_distance() {
        var result = Calculate(Trip(new DateTime(2026, 10, 1, 8, 0, 0), new DateTime(2026, 10, 1, 12, 0, 0), Car(30), Car(20)),
            TravelExpenseRulePresets.BadenWuerttemberg());

        await Assert.That(result.CarKm).IsEqualTo(50m);
        await Assert.That(result.CarAmount).IsEqualTo(16.50m);
    }

    [Test]
    public async Task Motorcycle_without_a_rate_is_not_reimbursed_and_flagged() {
        var leg = new TripModuleDTO { Kind = TripModuleKind.VehicleLeg, Vehicle = TripVehicle.Motorcycle, Km = 100 };
        var result = Calculate(Trip(new DateTime(2026, 10, 1, 8, 0, 0), new DateTime(2026, 10, 1, 12, 0, 0), leg),
            TravelExpenseRulePresets.BadenWuerttemberg());

        await Assert.That(result.MotorcycleAmount).IsEqualTo(0m);
        await Assert.That(result.Notes.Any(n => n.MessageKey == I18nKey.Ui.TravelExpense.Note.MotorcycleNotReimbursed && n.ModuleId == leg.Id)).IsTrue();
    }

    [Test]
    public async Task Hotel_breakfast_comes_off_as_itemised_or_as_the_breakfast_share() {
        var itemised = Hotel(new DateTime(2026, 10, 1, 18, 0, 0), new DateTime(2026, 10, 3, 9, 0, 0), 100);
        itemised.BreakfastIncluded = true;
        itemised.BreakfastAmountPerNight = 10;
        var flat = Hotel(new DateTime(2026, 10, 1, 18, 0, 0), new DateTime(2026, 10, 3, 9, 0, 0), 100);
        flat.BreakfastIncluded = true;

        var start = new DateTime(2026, 10, 1, 8, 0, 0);
        var end = new DateTime(2026, 10, 3, 20, 0, 0);
        await Assert.That(Calculate(Trip(start, end, itemised)).AccommodationReceiptAmount).IsEqualTo(80m);
        await Assert.That(Calculate(Trip(start, end, flat)).AccommodationReceiptAmount).IsEqualTo(88.80m);
    }

    [Test]
    public async Task Flat_rate_hotel_counts_nights() {
        var stay = Hotel(new DateTime(2026, 10, 1, 18, 0, 0), new DateTime(2026, 10, 4, 9, 0, 0), 0);
        stay.HotelByReceipt = false;

        var result = Calculate(Trip(new DateTime(2026, 10, 1, 8, 0, 0), new DateTime(2026, 10, 4, 20, 0, 0), stay));

        await Assert.That(result.AccommodationFlatNights).IsEqualTo(3);
        await Assert.That(result.AccommodationFlatAmount).IsEqualTo(60m);
    }

    [Test]
    public async Task Car_and_train_mix_in_one_trip() {
        var train = new TripModuleDTO { Kind = TripModuleKind.TransitLeg, TransitMode = TripTransitMode.Train, Amount = 49.90m };
        var result = Calculate(Trip(new DateTime(2026, 10, 1, 7, 0, 0), new DateTime(2026, 10, 1, 19, 0, 0), Car(20), train));

        await Assert.That(result.TravelAmount).IsEqualTo(55.90m);
        await Assert.That(result.Total).IsEqualTo(69.90m);
    }

    [Test]
    public async Task Donation_modes() {
        var input = Trip(new DateTime(2026, 10, 1, 7, 0, 0), new DateTime(2026, 10, 1, 19, 0, 0), Car(100));

        input.DonationMode = TravelDonationMode.All;
        await Assert.That(Calculate(input).Payout).IsEqualTo(0m);

        input.DonationMode = TravelDonationMode.Amount;
        input.DonationAmount = 500;
        await Assert.That(Calculate(input).Donation).IsEqualTo(44m);

        input.DonationMode = TravelDonationMode.Parts;
        input.DonateMealAllowance = true;
        var parts = Calculate(input);
        await Assert.That(parts.Donation).IsEqualTo(14m);
        await Assert.That(parts.Payout).IsEqualTo(30m);
    }

    [Test]
    public async Task Late_application_gets_notes_but_is_still_calculated() {
        var input = Trip(new DateTime(2025, 6, 1, 7, 0, 0), new DateTime(2025, 6, 1, 19, 0, 0), Car(100));

        var result = Calculate(input, today: new DateOnly(2026, 3, 1));

        await Assert.That(result.Total).IsEqualTo(44m);
        await Assert.That(result.Notes.Any(n => n.MessageKey.StartsWith(I18nKey.Ui.TravelExpense.Note.DeadlinePassed))).IsTrue();
        await Assert.That(result.Notes.Any(n => n.MessageKey.StartsWith(I18nKey.Ui.TravelExpense.Note.CutoffPassed))).IsTrue();
    }

    [Test]
    public async Task Complete_application_has_no_problems() {
        var result = Calculate(Trip(new DateTime(2026, 10, 1, 7, 0, 0), new DateTime(2026, 10, 1, 19, 0, 0), Car(100)));

        await Assert.That(result.Notes.Any(n => n.Severity == TravelExpenseNoteSeverity.Problem)).IsFalse();
    }

    [Test]
    public async Task Missing_iban_with_a_payout_is_a_problem() {
        var input = Trip(new DateTime(2026, 10, 1, 7, 0, 0), new DateTime(2026, 10, 1, 19, 0, 0), Car(100));
        input.Iban = "";

        var result = Calculate(input);

        await Assert.That(result.Notes.Any(n => n.MessageKey == I18nKey.Ui.TravelExpense.Note.IbanMissing)).IsTrue();
    }

    [Test]
    public async Task Leg_outside_the_trip_is_flagged() {
        var leg = Car(10);
        leg.At = new DateTime(2026, 9, 30, 8, 0, 0);

        var result = Calculate(Trip(new DateTime(2026, 10, 1, 7, 0, 0), new DateTime(2026, 10, 1, 19, 0, 0), leg));

        await Assert.That(result.Notes.Any(n => n.MessageKey == I18nKey.Ui.TravelExpense.Note.ModuleOutsideTrip && n.ModuleId == leg.Id)).IsTrue();
    }

    [Test]
    public async Task Cutoff_on_the_29th_falls_back_to_the_last_day_of_february() {
        var rules = TravelExpenseRulePresets.Federal();
        rules.CutoffDay = 29;

        await Assert.That(TravelExpenseCalculator.Cutoff(new DateOnly(2026, 5, 1), rules)).IsEqualTo(new DateOnly(2027, 2, 28));
        await Assert.That(TravelExpenseCalculator.Cutoff(new DateOnly(2027, 5, 1), rules)).IsEqualTo(new DateOnly(2028, 2, 29));
    }
}
