using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.TravelExpenses;

namespace Quartermaster.Blazor.Pages.Forms.TravelExpenses;

public partial class TravelExpenseOverviewStep {
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public TravelExpenseInputDTO Input { get; set; } = default!;

    [Parameter, EditorRequired]
    public TravelExpenseRulesDTO Rules { get; set; } = default!;

    [Parameter, EditorRequired]
    public TravelExpenseCalculationDTO Calculation { get; set; } = default!;

    /// <summary>The module's title followed by whatever details it has, so empty fields leave no stray separators.</summary>
    private string Describe(TripModuleDTO module) {
        var title = I18n[TripModuleLabels.TitleKey(module)];
        var (detail, extra) = module.Kind switch {
            TripModuleKind.VehicleLeg => (Route(module), $"{module.Km.ToString("0.#", German)} km"),
            TripModuleKind.TransitLeg => (Route(module), ""),
            TripModuleKind.HotelStay => (module.Description, NightsText(module)),
            TripModuleKind.ProvidedMeal => ("", ""),
            _ => (module.Description, "")
        };

        var text = string.IsNullOrWhiteSpace(detail) ? title : $"{title}: {detail}";
        return string.IsNullOrEmpty(extra) ? text : $"{text} ({extra})";
    }

    private static string Route(TripModuleDTO module) {
        return string.Join(" - ", new[] { module.From, module.To }.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    /// <summary>Meals and costs entered with a date only show no time.</summary>
    private static string When(TripModuleDTO module) {
        if (module.At == null)
            return "";
        if (module.Kind == TripModuleKind.ProvidedMeal || module.At.Value.TimeOfDay == TimeSpan.Zero)
            return module.At.Value.ToString("dd.MM.yyyy", German);

        return DateAndTime(module.At);
    }

    private Dictionary<Guid, string> ModuleLabels() {
        return Input.Modules.ToDictionary(m => m.Id, m => I18n[TripModuleLabels.TitleKey(m)]);
    }

    private string NightsText(TripModuleDTO module) {
        var nights = TravelExpenseCalculator.Nights(module);
        return I18n[I18nParams.With(TripModuleLabels.NightsKey(nights), ("nights", nights.ToString(German)))];
    }

    private string ModuleAmount(TripModuleDTO module) {
        return Calculation.ModuleAmounts.TryGetValue(module.Id, out var amount) ? Money(amount) : "";
    }

    private static string DateAndTime(DateTime? value) {
        return value?.ToString("dd.MM.yyyy HH:mm", German) ?? "";
    }

    private static string Money(decimal value) {
        return value.ToString("N2", German) + " €";
    }
}
