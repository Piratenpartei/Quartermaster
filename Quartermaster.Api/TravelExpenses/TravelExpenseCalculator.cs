using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Quartermaster.Api.Banking;
using Quartermaster.Api.I18n;

namespace Quartermaster.Api.TravelExpenses;

/// <summary>
/// Computes a travel expense application from its input and the applicable rules. Meal allowances follow § 9 Abs. 4a
/// EStG: per calendar day of absence from home, reduced for meals provided by the party or an organiser by a share of
/// the full-day rate. Notes point out what will likely lead to questions; they never block.
/// </summary>
public static class TravelExpenseCalculator {
    private const double DayTripMinimumHours = 8;

    public static TravelExpenseCalculationDTO Calculate(TravelExpenseInputDTO input, TravelExpenseRulesDTO rules, DateOnly today) {
        var result = new TravelExpenseCalculationDTO();

        CalculateMeals(input, rules, result);
        CalculateTravel(input, rules, result);
        CalculateAccommodation(input, rules, result);
        CalculateOtherCosts(input, result);

        result.TravelAmount = result.CarAmount + result.MotorcycleAmount + result.TicketsAmount;
        result.AccommodationAmount = result.AccommodationReceiptAmount + result.AccommodationFlatAmount;
        result.Total = result.TravelAmount + result.MealAllowance + result.AccommodationAmount + result.OtherCostsAmount;
        result.Donation = Donation(input, result);
        result.Payout = result.Total - result.Donation;

        AddNotes(input, rules, result, today);
        return result;
    }

    /// <summary>The amount for a distance under per-km rate steps; each step's limit is a total distance.</summary>
    public static decimal MileageAmount(decimal km, IReadOnlyList<RateStepDTO> steps) {
        var amount = 0m;
        var remaining = km;
        var covered = 0m;
        foreach (var step in steps) {
            if (remaining <= 0)
                break;

            var portion = step.UpToKm == null ? remaining : Math.Min(remaining, Math.Max(0, step.UpToKm.Value - covered));
            amount += portion * step.RatePerKm;
            remaining -= portion;
            covered += portion;
        }
        return Round(amount);
    }

    private static void CalculateMeals(TravelExpenseInputDTO input, TravelExpenseRulesDTO rules, TravelExpenseCalculationDTO result) {
        result.MealDays = MealDays(input, rules);
        var byDate = result.MealDays.ToDictionary(d => d.Date);

        var meals = input.Modules
            .Where(m => m.Kind == TripModuleKind.ProvidedMeal && m.At != null)
            .OrderBy(m => m.At);
        foreach (var meal in meals) {
            if (!byDate.TryGetValue(DateOnly.FromDateTime(meal.At!.Value), out var day)) {
                result.ModuleAmounts[meal.Id] = 0;
                continue;
            }

            var reduction = Math.Min(Round(ReductionPercent(meal.Meal, rules) / 100m * rules.MealFullDayRate), day.Allowance - day.Reduction);
            day.Reduction += reduction;
            result.ModuleAmounts[meal.Id] = -reduction;
        }

        foreach (var day in result.MealDays)
            day.Net = day.Allowance - day.Reduction;

        result.ArrivalDepartureDays = result.MealDays.Count(d => d.Kind == MealDayKind.ArrivalDeparture || d.Kind == MealDayKind.DayTrip);
        result.FullDays = result.MealDays.Count(d => d.Kind == MealDayKind.FullDay);
        result.MealAllowanceGross = result.MealDays.Sum(d => d.Allowance);
        result.MealReductions = result.MealDays.Sum(d => d.Reduction);
        result.MealAllowance = result.MealAllowanceGross - result.MealReductions;
    }

    // A trip over midnight shorter than a day and without a hotel stay counts as one day trip on the calendar day with the
    // larger share of the absence (§ 9 Abs. 4a Satz 3 Nr. 3 EStG).
    private static List<MealDayDTO> MealDays(TravelExpenseInputDTO input, TravelExpenseRulesDTO rules) {
        var days = new List<MealDayDTO>();
        if (input.Start.At == null || input.End.At == null || input.End.At <= input.Start.At)
            return days;

        var start = input.Start.At.Value;
        var end = input.End.At.Value;
        var startDay = DateOnly.FromDateTime(start);
        var endDay = DateOnly.FromDateTime(end);
        var duration = end - start;

        if (startDay == endDay) {
            days.Add(DayTripOrNone(startDay, duration.TotalHours, rules));
            return days;
        }

        var hasHotel = input.Modules.Any(m => m.Kind == TripModuleKind.HotelStay);
        if (endDay == startDay.AddDays(1) && !hasHotel && duration < TimeSpan.FromHours(24)) {
            var midnight = end.Date;
            var mainDay = midnight - start >= end - midnight ? startDay : endDay;
            var otherDay = mainDay == startDay ? endDay : startDay;
            days.Add(DayTripOrNone(mainDay, duration.TotalHours, rules));
            days.Add(new MealDayDTO { Date = otherDay, Kind = MealDayKind.None });
            return days.OrderBy(d => d.Date).ToList();
        }

        for (var day = startDay; day <= endDay; day = day.AddDays(1)) {
            var isEdge = day == startDay || day == endDay;
            days.Add(new MealDayDTO {
                Date = day,
                Kind = isEdge ? MealDayKind.ArrivalDeparture : MealDayKind.FullDay,
                Allowance = isEdge ? rules.MealArrivalDepartureRate : rules.MealFullDayRate
            });
        }
        return days;
    }

    private static MealDayDTO DayTripOrNone(DateOnly date, double hours, TravelExpenseRulesDTO rules) {
        return hours > DayTripMinimumHours
            ? new MealDayDTO { Date = date, Kind = MealDayKind.DayTrip, Allowance = rules.MealDayTripRate }
            : new MealDayDTO { Date = date, Kind = MealDayKind.None };
    }

    private static decimal ReductionPercent(TripMeal meal, TravelExpenseRulesDTO rules) {
        return meal switch {
            TripMeal.Breakfast => rules.BreakfastReductionPercent,
            TripMeal.Lunch => rules.LunchReductionPercent,
            _ => rules.DinnerReductionPercent
        };
    }

    private static void CalculateTravel(TravelExpenseInputDTO input, TravelExpenseRulesDTO rules, TravelExpenseCalculationDTO result) {
        var vehicleLegs = input.Modules.Where(m => m.Kind == TripModuleKind.VehicleLeg).ToList();
        var carLegs = vehicleLegs.Where(m => m.Vehicle == TripVehicle.Car).ToList();
        var motorcycleLegs = vehicleLegs.Where(m => m.Vehicle == TripVehicle.Motorcycle).ToList();

        result.CarKm = carLegs.Sum(m => Math.Max(0, m.Km));
        result.CarAmount = MileageAmount(result.CarKm, rules.CarRates);
        result.MotorcycleKm = motorcycleLegs.Sum(m => Math.Max(0, m.Km));
        result.MotorcycleAmount = MileageAmount(result.MotorcycleKm, rules.MotorcycleRates);
        AssignLegShares(carLegs, result.CarKm, result.CarAmount, result);
        AssignLegShares(motorcycleLegs, result.MotorcycleKm, result.MotorcycleAmount, result);

        foreach (var leg in input.Modules.Where(m => m.Kind == TripModuleKind.TransitLeg)) {
            var amount = Math.Max(0, leg.Amount);
            result.TicketsAmount += amount;
            result.ModuleAmounts[leg.Id] = amount;
        }
    }

    // With rate steps the amount belongs to the whole distance, so each leg shows its share of it.
    private static void AssignLegShares(List<TripModuleDTO> legs, decimal totalKm, decimal totalAmount, TravelExpenseCalculationDTO result) {
        foreach (var leg in legs)
            result.ModuleAmounts[leg.Id] = totalKm == 0 ? 0 : Round(totalAmount * Math.Max(0, leg.Km) / totalKm);
    }

    private static void CalculateAccommodation(TravelExpenseInputDTO input, TravelExpenseRulesDTO rules, TravelExpenseCalculationDTO result) {
        foreach (var stay in input.Modules.Where(m => m.Kind == TripModuleKind.HotelStay)) {
            var nights = Nights(stay);
            if (!stay.HotelByReceipt) {
                var flat = nights * rules.AccommodationFlatRate;
                result.AccommodationFlatNights += nights;
                result.AccommodationFlatAmount += flat;
                result.ModuleAmounts[stay.Id] = flat;
                continue;
            }

            var breakfast = stay.BreakfastIncluded ? BreakfastPerNight(stay, rules) * nights : 0;
            var net = Math.Max(0, stay.Amount - breakfast);
            result.AccommodationReceiptAmount += net;
            result.ModuleAmounts[stay.Id] = net;
        }
    }

    public static int Nights(TripModuleDTO stay) {
        if (stay.At == null || stay.Until == null)
            return 1;

        return Math.Max(1, (stay.Until.Value.Date - stay.At.Value.Date).Days);
    }

    /// <summary>The itemised breakfast price, otherwise the breakfast share of the full-day meal allowance.</summary>
    public static decimal BreakfastPerNight(TripModuleDTO stay, TravelExpenseRulesDTO rules) {
        return stay.BreakfastAmountPerNight ?? Round(rules.BreakfastReductionPercent / 100m * rules.MealFullDayRate);
    }

    private static void CalculateOtherCosts(TravelExpenseInputDTO input, TravelExpenseCalculationDTO result) {
        foreach (var cost in input.Modules.Where(m => m.Kind == TripModuleKind.OtherCost)) {
            var amount = Math.Max(0, cost.Amount);
            result.OtherCosts.Add(new OtherCostLineDTO { Description = cost.Description, Amount = amount });
            result.OtherCostsAmount += amount;
            result.ModuleAmounts[cost.Id] = amount;
        }
    }

    private static decimal Donation(TravelExpenseInputDTO input, TravelExpenseCalculationDTO result) {
        return input.DonationMode switch {
            TravelDonationMode.All => result.Total,
            TravelDonationMode.Amount => Math.Clamp(input.DonationAmount, 0, result.Total),
            TravelDonationMode.Parts => DonatedParts(input, result),
            _ => 0
        };
    }

    private static decimal DonatedParts(TravelExpenseInputDTO input, TravelExpenseCalculationDTO result) {
        var donation = 0m;
        if (input.DonateTravel)
            donation += result.TravelAmount;
        if (input.DonateMealAllowance)
            donation += result.MealAllowance;
        if (input.DonateAccommodation)
            donation += result.AccommodationAmount;
        if (input.DonateOther)
            donation += result.OtherCostsAmount;

        return donation;
    }

    private static void AddNotes(TravelExpenseInputDTO input, TravelExpenseRulesDTO rules, TravelExpenseCalculationDTO result, DateOnly today) {
        var notes = new List<TravelExpenseNoteDTO>();
        AddPersonNotes(input, result, notes);
        AddTripNotes(input, rules, result, today, notes);
        foreach (var module in input.Modules)
            AddModuleNotes(input, rules, module, notes);

        result.Notes = notes
            .OrderByDescending(n => n.Severity)
            .ToList();
    }

    private static void AddPersonNotes(TravelExpenseInputDTO input, TravelExpenseCalculationDTO result, List<TravelExpenseNoteDTO> notes) {
        if (string.IsNullOrWhiteSpace(input.FirstName) || string.IsNullOrWhiteSpace(input.LastName))
            notes.Add(Note(TravelExpenseNoteSeverity.Problem, I18nKey.Ui.TravelExpense.Note.NameMissing));
        if (string.IsNullOrWhiteSpace(input.Street) || string.IsNullOrWhiteSpace(input.PostCode) || string.IsNullOrWhiteSpace(input.City))
            notes.Add(Note(TravelExpenseNoteSeverity.Problem, I18nKey.Ui.TravelExpense.Note.AddressIncomplete));
        if (input.MemberNumber == null)
            notes.Add(Note(TravelExpenseNoteSeverity.Warning, I18nKey.Ui.TravelExpense.Note.MemberNumberMissing));

        if (result.Payout > 0 && string.IsNullOrWhiteSpace(input.Iban)) {
            notes.Add(Note(TravelExpenseNoteSeverity.Problem, I18nKey.Ui.TravelExpense.Note.IbanMissing));
        } else if (!string.IsNullOrWhiteSpace(input.Iban) && !Iban.IsValid(input.Iban)) {
            notes.Add(Note(TravelExpenseNoteSeverity.Problem, I18nKey.Ui.TravelExpense.Note.IbanInvalid));
        }
    }

    private static void AddTripNotes(TravelExpenseInputDTO input, TravelExpenseRulesDTO rules, TravelExpenseCalculationDTO result, DateOnly today,
        List<TravelExpenseNoteDTO> notes) {
        if (string.IsNullOrWhiteSpace(input.Purpose))
            notes.Add(Note(TravelExpenseNoteSeverity.Problem, I18nKey.Ui.TravelExpense.Note.PurposeMissing));
        if (string.IsNullOrWhiteSpace(input.ResolutionNumber))
            notes.Add(Note(TravelExpenseNoteSeverity.Info, I18nKey.Ui.TravelExpense.Note.ResolutionMissing));
        if (result.Total == 0)
            notes.Add(Note(TravelExpenseNoteSeverity.Problem, I18nKey.Ui.TravelExpense.Note.NoCosts));

        if (input.Start.At == null || input.End.At == null) {
            notes.Add(Note(TravelExpenseNoteSeverity.Problem, I18nKey.Ui.TravelExpense.Note.TimesMissing));
            return;
        }
        if (input.End.At <= input.Start.At) {
            notes.Add(Note(TravelExpenseNoteSeverity.Problem, I18nKey.Ui.TravelExpense.Note.EndBeforeStart));
            return;
        }

        var endDay = DateOnly.FromDateTime(input.End.At.Value);
        if (rules.SubmissionDeadlineDays > 0 && today > endDay.AddDays(rules.SubmissionDeadlineDays)) {
            notes.Add(Note(TravelExpenseNoteSeverity.Warning, I18nParams.With(I18nKey.Ui.TravelExpense.Note.DeadlinePassed,
                ("days", rules.SubmissionDeadlineDays.ToString(CultureInfo.InvariantCulture)))));
        }

        var cutoff = Cutoff(endDay, rules);
        if (cutoff != null && today > cutoff.Value) {
            notes.Add(Note(TravelExpenseNoteSeverity.Warning, I18nParams.With(I18nKey.Ui.TravelExpense.Note.CutoffPassed,
                ("date", cutoff.Value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)))));
        }
    }

    /// <summary>The day in the year after the trip from which a reimbursement needs a board decision; null when the rules set none.</summary>
    public static DateOnly? Cutoff(DateOnly tripEnd, TravelExpenseRulesDTO rules) {
        if (rules.CutoffMonth < 1 || rules.CutoffMonth > 12 || rules.CutoffDay < 1)
            return null;

        var year = tripEnd.Year + 1;
        var day = Math.Min(rules.CutoffDay, DateTime.DaysInMonth(year, rules.CutoffMonth));
        return new DateOnly(year, rules.CutoffMonth, day);
    }

    private static void AddModuleNotes(TravelExpenseInputDTO input, TravelExpenseRulesDTO rules, TripModuleDTO module, List<TravelExpenseNoteDTO> notes) {
        if (module.Kind == TripModuleKind.VehicleLeg && module.Vehicle == TripVehicle.Motorcycle && rules.MotorcycleRates.Count == 0)
            notes.Add(Note(TravelExpenseNoteSeverity.Problem, I18nKey.Ui.TravelExpense.Note.MotorcycleNotReimbursed, module.Id));
        if (module.Kind == TripModuleKind.VehicleLeg && module.Km <= 0)
            notes.Add(Note(TravelExpenseNoteSeverity.Warning, I18nKey.Ui.TravelExpense.Note.VehicleLegWithoutKm, module.Id));
        if (NeedsAmount(module) && module.Amount <= 0)
            notes.Add(Note(TravelExpenseNoteSeverity.Warning, I18nKey.Ui.TravelExpense.Note.AmountMissing, module.Id));
        if (IsOutsideTrip(input, module))
            notes.Add(Note(TravelExpenseNoteSeverity.Warning, I18nKey.Ui.TravelExpense.Note.ModuleOutsideTrip, module.Id));
        if (OverlapsAnotherLeg(input, module))
            notes.Add(Note(TravelExpenseNoteSeverity.Warning, I18nKey.Ui.TravelExpense.Note.LegsOverlap, module.Id));
    }

    private static bool NeedsAmount(TripModuleDTO module) {
        if (module.Kind == TripModuleKind.TransitLeg)
            return true;
        if (module.Kind == TripModuleKind.OtherCost)
            return true;

        return module.Kind == TripModuleKind.HotelStay && module.HotelByReceipt;
    }

    private static bool IsOutsideTrip(TravelExpenseInputDTO input, TripModuleDTO module) {
        if (input.Start.At == null || input.End.At == null || module.At == null)
            return false;
        if (module.Kind == TripModuleKind.ProvidedMeal || module.Kind == TripModuleKind.OtherCost)
            return module.At.Value.Date < input.Start.At.Value.Date || module.At.Value.Date > input.End.At.Value.Date;
        if (module.At < input.Start.At)
            return true;

        return module.Until != null && module.Until > input.End.At;
    }

    private static bool OverlapsAnotherLeg(TravelExpenseInputDTO input, TripModuleDTO module) {
        if (!IsTimedLeg(module))
            return false;

        return input.Modules
            .Where(other => other.Id != module.Id && IsTimedLeg(other))
            .Any(other => module.At < other.Until && other.At < module.Until);
    }

    private static bool IsTimedLeg(TripModuleDTO module) {
        if (module.Kind != TripModuleKind.VehicleLeg && module.Kind != TripModuleKind.TransitLeg)
            return false;

        return module.At != null && module.Until != null && module.Until > module.At;
    }

    private static TravelExpenseNoteDTO Note(TravelExpenseNoteSeverity severity, string messageKey, Guid? moduleId = null) {
        return new TravelExpenseNoteDTO { Severity = severity, MessageKey = messageKey, ModuleId = moduleId };
    }

    private static decimal Round(decimal amount) {
        return Math.Round(amount, 2, MidpointRounding.AwayFromZero);
    }
}
