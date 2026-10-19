using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Quartermaster.Api.I18n;
using Quartermaster.Api.TravelExpenses;

namespace Quartermaster.Server.TravelExpenses;

/// <summary>Turns an application, its rules and the calculation into the print-ready <see cref="TravelExpensePdfModel"/>.</summary>
public static class TravelExpensePdfModelBuilder {
    private const int OfficialOtherCostLines = 3;
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    public static TravelExpensePdfModel Build(TravelExpenseInputDTO input, TravelExpenseRulesDTO rules, TravelExpenseCalculationDTO calc,
        int receiptCount, I18nService i18n) {
        var model = new TravelExpensePdfModel {
            VersionLabel = rules.VersionLabel,
            ValidFrom = rules.ValidFrom.ToString("dd.MM.yyyy", German),
            HeaderLines = Lines(rules.HeaderLines),
            Name = $"{input.FirstName} {input.LastName}".Trim(),
            MemberNumber = input.MemberNumber?.ToString(German) ?? "",
            Street = input.Street,
            PostCodeCity = $"{input.PostCode} {input.City}".Trim(),
            Email = input.Email,
            Function = input.Function,
            MembershipChapter = input.MembershipChapterName,
            TravelChapter = input.TravelChapterName,
            Purpose = input.Purpose,
            ResolutionNumber = input.ResolutionNumber,
            ResolutionDate = Date(input.ResolutionDate),
            Destination = input.Destination,
            StartDate = Date(input.Start.At),
            StartTime = Time(input.Start.At),
            EndDate = Date(input.End.At),
            EndTime = Time(input.End.At),
            CarRate = RateText(rules.CarRates, i18n),
            MotorcycleRate = RateText(rules.MotorcycleRates, i18n),
            DayTripRate = Money(rules.MealDayTripRate),
            ArrivalDepartureRate = Money(rules.MealArrivalDepartureRate),
            FullDayRate = Money(rules.MealFullDayRate),
            AccommodationFlatRate = Money(rules.AccommodationFlatRate),
            Iban = input.Iban,
            Bic = input.Bic,
            AttachmentCount = input.PaperAttachmentCount + receiptCount,
            FormDate = input.FormDate.ToString("dd.MM.yyyy", German),
            RulesParagraphs = Paragraphs(rules.RulesText)
        };

        FillCosts(model, calc, i18n);
        model.Timeline = Timeline(input, calc, i18n);
        model.MealDays = calc.MealDays
            .Select(d => new TravelExpensePdfMealDay {
                Date = d.Date.ToString("ddd, dd.MM.yyyy", German),
                Kind = i18n[TripModuleLabels.MealDayKey(d.Kind)],
                Allowance = Money(d.Allowance),
                Reduction = d.Reduction == 0 ? "" : "-" + Money(d.Reduction),
                Net = Money(d.Net)
            })
            .ToList();
        model.Notes = calc.Notes
            .Select(n => new TravelExpensePdfLine { Description = i18n[SeverityKey(n.Severity)], Amount = i18n[n.MessageKey] })
            .ToList();
        return model;
    }

    private static void FillCosts(TravelExpensePdfModel model, TravelExpenseCalculationDTO calc, I18nService i18n) {
        model.Tickets = AmountOrEmpty(calc.TicketsAmount);
        var hasCar = calc.CarKm > 0;
        var hasMotorcycle = calc.MotorcycleKm > 0;
        model.MileageKm = string.Join(" / ", new[] { hasCar ? Number(calc.CarKm) : null, hasMotorcycle ? Number(calc.MotorcycleKm) : null }.Where(t => t != null));
        model.MileageRate = string.Join(" / ", new[] { hasCar ? model.CarRate : null, hasMotorcycle ? model.MotorcycleRate : null }.Where(t => t != null));
        model.Mileage = AmountOrEmpty(calc.CarAmount + calc.MotorcycleAmount);

        model.ArrivalDepartureDays = calc.ArrivalDepartureDays;
        model.ArrivalDepartureAmount = Amount(calc.MealDays.Where(d => d.Kind != MealDayKind.FullDay).Sum(d => d.Allowance));
        model.FullDays = calc.FullDays;
        model.FullDaysAmount = Amount(calc.MealDays.Where(d => d.Kind == MealDayKind.FullDay).Sum(d => d.Allowance));
        model.MealReductions = calc.MealReductions == 0 ? "" : "-" + Amount(calc.MealReductions);
        model.MealAllowance = Amount(calc.MealAllowance);

        model.AccommodationReceipt = AmountOrEmpty(calc.AccommodationReceiptAmount);
        model.AccommodationFlatNights = calc.AccommodationFlatNights;
        model.AccommodationFlat = AmountOrEmpty(calc.AccommodationFlatAmount);

        model.AllOtherCosts = calc.OtherCosts
            .Select(c => new TravelExpensePdfLine { Description = c.Description, Amount = Amount(c.Amount) })
            .ToList();
        model.OtherCosts = calc.OtherCosts.Count <= OfficialOtherCostLines
            ? model.AllOtherCosts
            : model.AllOtherCosts
                .Take(OfficialOtherCostLines - 1)
                .Append(new TravelExpensePdfLine {
                    Description = i18n[I18nKey.Ui.TravelExpense.Pdf.MoreOtherCosts],
                    Amount = Amount(calc.OtherCosts.Skip(OfficialOtherCostLines - 1).Sum(c => c.Amount))
                })
                .ToList();

        model.Total = Amount(calc.Total);
        model.Donation = Amount(calc.Donation);
        model.Payout = Amount(calc.Payout);
    }

    private static List<TravelExpensePdfTimelineRow> Timeline(TravelExpenseInputDTO input, TravelExpenseCalculationDTO calc, I18nService i18n) {
        var rows = new List<TravelExpensePdfTimelineRow> {
            new() { When = DateAndTime(input.Start.At), Title = i18n[I18nKey.Ui.TravelExpense.Module.Start], Detail = input.Start.Place }
        };
        var modules = input.Modules
            .OrderBy(TripModuleOrder.SortKey);
        foreach (var module in modules) {
            rows.Add(new TravelExpensePdfTimelineRow {
                When = module.Kind == TripModuleKind.ProvidedMeal || module.Kind == TripModuleKind.OtherCost ? Date(module.At) : DateAndTime(module.At),
                Title = i18n[TripModuleLabels.TitleKey(module)],
                Detail = ModuleDetail(module, i18n),
                Amount = calc.ModuleAmounts.TryGetValue(module.Id, out var amount) ? Money(amount) : ""
            });
        }
        rows.Add(new TravelExpensePdfTimelineRow { When = DateAndTime(input.End.At), Title = i18n[I18nKey.Ui.TravelExpense.Module.End], Detail = input.End.Place });
        return rows;
    }

    private static string ModuleDetail(TripModuleDTO module, I18nService i18n) {
        var route = string.IsNullOrWhiteSpace(module.From) && string.IsNullOrWhiteSpace(module.To) ? "" : $"{module.From} → {module.To}";
        return module.Kind switch {
            TripModuleKind.VehicleLeg => Join(route, $"{Number(module.Km)} km"),
            TripModuleKind.TransitLeg => Join(route, module.Description),
            TripModuleKind.HotelStay => Join(module.Description, HotelDetail(module, i18n)),
            _ => module.Description
        };
    }

    private static string HotelDetail(TripModuleDTO module, I18nService i18n) {
        var count = TravelExpenseCalculator.Nights(module);
        var nights = i18n[I18nParams.With(TripModuleLabels.NightsKey(count), ("nights", count.ToString(German)))];
        if (!module.HotelByReceipt)
            return i18n[I18nParams.With(I18nKey.Ui.TravelExpense.Pdf.HotelFlatNights, ("nights", nights))];

        var receipt = i18n[I18nParams.With(I18nKey.Ui.TravelExpense.Pdf.HotelReceiptNights, ("nights", nights))];
        return module.BreakfastIncluded ? Join(receipt, i18n[I18nKey.Ui.TravelExpense.Pdf.BreakfastDeducted]) : receipt;
    }

    private static string Join(params string[] parts) {
        return string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    /// <summary>"0,30 €", or "0,30 € bis 20 km, danach 0,35 €" for rate steps; empty when the rules have no rate.</summary>
    private static string RateText(IReadOnlyList<RateStepDTO> steps, I18nService i18n) {
        if (steps.Count == 0)
            return "";
        if (steps.Count == 1)
            return Money(steps[0].RatePerKm);

        var parts = steps.Select((step, index) => step.UpToKm == null || index == steps.Count - 1
            ? i18n[I18nParams.With(I18nKey.Ui.TravelExpense.Pdf.RateThen, ("rate", Money(step.RatePerKm)))]
            : i18n[I18nParams.With(I18nKey.Ui.TravelExpense.Pdf.RateUpTo, ("rate", Money(step.RatePerKm)), ("km", Number(step.UpToKm.Value)))]);
        return string.Join(", ", parts);
    }

    private static string SeverityKey(TravelExpenseNoteSeverity severity) {
        return severity switch {
            TravelExpenseNoteSeverity.Problem => I18nKey.Ui.TravelExpense.Severity.Problem,
            TravelExpenseNoteSeverity.Warning => I18nKey.Ui.TravelExpense.Severity.Warning,
            _ => I18nKey.Ui.TravelExpense.Severity.Info
        };
    }

    private static List<string> Lines(string text) {
        return text
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();
    }

    private static List<string> Paragraphs(string text) {
        return text
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();
    }

    private static string Date(DateTime? value) {
        return value?.ToString("dd.MM.yyyy", German) ?? "";
    }

    private static string Time(DateTime? value) {
        return value?.ToString("HH:mm", German) ?? "";
    }

    private static string DateAndTime(DateTime? value) {
        return value?.ToString("dd.MM.yyyy HH:mm", German) ?? "";
    }

    private static string Amount(decimal value) {
        return value.ToString("N2", German);
    }

    private static string AmountOrEmpty(decimal value) {
        return value == 0 ? "" : Amount(value);
    }

    private static string Money(decimal value) {
        return value.ToString("N2", German) + " €";
    }

    private static string Number(decimal value) {
        return value.ToString("0.##", German);
    }
}
