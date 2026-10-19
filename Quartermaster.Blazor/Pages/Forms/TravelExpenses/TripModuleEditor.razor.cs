using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.TravelExpenses;

namespace Quartermaster.Blazor.Pages.Forms.TravelExpenses;

public partial class TripModuleEditor {
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");
    private static readonly TripTransitMode[] TransitModes = Enum.GetValues<TripTransitMode>();
    private static readonly TripMeal[] Meals = Enum.GetValues<TripMeal>();

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public TripModuleDTO Module { get; set; } = default!;

    [Parameter, EditorRequired]
    public TravelExpenseRulesDTO Rules { get; set; } = default!;

    [Parameter, EditorRequired]
    public TravelExpenseCalculationDTO Calculation { get; set; } = default!;

    [Parameter]
    public bool CanUseReceipts { get; set; }

    [Parameter]
    public EventCallback OnChanged { get; set; }

    [Parameter]
    public EventCallback OnRemove { get; set; }

    private Task Changed() {
        return OnChanged.InvokeAsync();
    }

    private Task SetVehicle(TripVehicle vehicle) {
        Module.Vehicle = vehicle;
        return Changed();
    }

    private Task SetTransit(TripTransitMode mode) {
        Module.TransitMode = mode;
        return Changed();
    }

    private Task SetByReceipt(bool byReceipt) {
        Module.HotelByReceipt = byReceipt;
        return Changed();
    }

    private Task SetMeal(TripMeal meal) {
        Module.Meal = meal;
        return Changed();
    }

    // A motorcycle leg chosen under other rules stays selectable, so the "not reimbursed" note can explain it.
    private bool OffersMotorcycle() {
        return Rules.MotorcycleRates.Count > 0 || Module.Vehicle == TripVehicle.Motorcycle;
    }

    private List<TravelExpenseNoteDTO> ModuleNotes() {
        return Calculation.Notes
            .Where(n => n.ModuleId == Module.Id)
            .ToList();
    }

    private bool HasProblem() {
        return Calculation.Notes.Any(n => n.ModuleId == Module.Id && n.Severity == TravelExpenseNoteSeverity.Problem);
    }

    private string AmountText() {
        return Calculation.ModuleAmounts.TryGetValue(Module.Id, out var amount) ? Money(amount) : "";
    }

    private decimal BreakfastShare() {
        return Math.Round(Rules.BreakfastReductionPercent / 100m * Rules.MealFullDayRate, 2, MidpointRounding.AwayFromZero);
    }

    private string Icon() {
        return TripModuleIcons.For(Module);
    }

    private string TitleKey() {
        return TripModuleLabels.TitleKey(Module);
    }

    private static string TransitKey(TripTransitMode mode) {
        return mode switch {
            TripTransitMode.Train => I18nKey.Ui.TravelExpense.TransitTrain,
            TripTransitMode.Bus => I18nKey.Ui.TravelExpense.TransitBus,
            TripTransitMode.Plane => I18nKey.Ui.TravelExpense.TransitPlane,
            _ => I18nKey.Ui.TravelExpense.TransitOther
        };
    }

    private static string MealKey(TripMeal meal) {
        return meal switch {
            TripMeal.Breakfast => I18nKey.Ui.TravelExpense.MealBreakfast,
            TripMeal.Lunch => I18nKey.Ui.TravelExpense.MealLunch,
            _ => I18nKey.Ui.TravelExpense.MealDinner
        };
    }

    private static string Money(decimal value) {
        return value.ToString("N2", German) + " €";
    }
}
