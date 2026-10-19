using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.TravelExpenses;

namespace Quartermaster.Blazor.Pages.Forms.TravelExpenses;

/// <summary>The trip as a timeline: start on top, end at the bottom and any mix of legs, stays, meals and costs between.</summary>
public partial class TripBuilder {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public TravelExpenseInputDTO Input { get; set; } = default!;

    [Parameter, EditorRequired]
    public TravelExpenseRulesDTO Rules { get; set; } = default!;

    [Parameter, EditorRequired]
    public TravelExpenseCalculationDTO Calculation { get; set; } = default!;

    [Parameter]
    public bool CanUseReceipts { get; set; }

    [Parameter]
    public EventCallback OnChanged { get; set; }

    /// <summary>The gap an add bar is open in, as the index of the entry above it; -1 is the start.</summary>
    private int? OpenGap;

    private static readonly string[] TripNoteKeys = [
        I18nKey.Ui.TravelExpense.Note.TimesMissing, I18nKey.Ui.TravelExpense.Note.EndBeforeStart, I18nKey.Ui.TravelExpense.Note.NoCosts,
        I18nKey.Ui.TravelExpense.Note.DeadlinePassed, I18nKey.Ui.TravelExpense.Note.CutoffPassed
    ];

    // The trip starts and ends at home unless the applicant says otherwise.
    protected override void OnInitialized() {
        if (string.IsNullOrWhiteSpace(Input.Start.Place))
            Input.Start.Place = Input.City;
        if (string.IsNullOrWhiteSpace(Input.End.Place))
            Input.End.Place = Input.City;
    }

    private List<TripModuleDTO> OrderedModules() {
        return Input.Modules
            .OrderBy(TripModuleOrder.SortKey)
            .ToList();
    }

    /// <summary>Notes about the trip as a whole; person and payout notes wait for the overview.</summary>
    private List<TravelExpenseNoteDTO> GeneralNotes() {
        return Calculation.Notes
            .Where(n => n.ModuleId == null && TripNoteKeys.Any(key => n.MessageKey == key || n.MessageKey.StartsWith(key + "?")))
            .ToList();
    }

    private Task Changed() {
        return OnChanged.InvokeAsync();
    }

    private async Task Remove(TripModuleDTO module) {
        Input.Modules.Remove(module);
        await Changed();
    }

    private void OpenGapAt(int afterIndex) {
        OpenGap = afterIndex;
    }

    private void CloseGap() {
        OpenGap = null;
    }

    /// <summary>
    /// Adds a module after the entry at <paramref name="afterIndex"/> (-1 for the start), timed where that entry ends.
    /// It goes into the list before the next entry, so a tie in time keeps it in the chosen place.
    /// </summary>
    private async Task Insert(TripModuleKind kind, int afterIndex) {
        var ordered = OrderedModules();
        var previous = afterIndex >= 0 && afterIndex < ordered.Count ? ordered[afterIndex] : null;
        var next = afterIndex + 1 < ordered.Count ? ordered[afterIndex + 1] : null;
        var reference = previous?.Until ?? previous?.At ?? Input.Start.At ?? DateTime.Today.AddHours(8);
        var module = new TripModuleDTO { Kind = kind, At = reference };

        switch (kind) {
            case TripModuleKind.VehicleLeg:
            case TripModuleKind.TransitLeg:
                module.From = previous == null ? Input.Start.Place : previous.To;
                module.To = next?.From ?? "";
                break;
            case TripModuleKind.HotelStay:
                if (next == null && reference.Hour < 18)
                    module.At = reference.Date.AddHours(18);
                module.Until = next?.At > reference ? next.At : reference.Date.AddDays(1).AddHours(9);
                break;
            case TripModuleKind.ProvidedMeal:
                module.At = reference.Date;
                module.Meal = MealAround(reference);
                break;
        }

        var position = next == null ? Input.Modules.Count : Input.Modules.IndexOf(next);
        Input.Modules.Insert(position, module);
        OpenGap = null;
        await Changed();
    }

    private static TripMeal MealAround(DateTime time) {
        if (time.Hour < 10)
            return TripMeal.Breakfast;
        if (time.Hour < 15)
            return TripMeal.Lunch;

        return TripMeal.Dinner;
    }
}
