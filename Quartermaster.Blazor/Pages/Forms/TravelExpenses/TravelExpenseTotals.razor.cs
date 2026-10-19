using System.Globalization;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.TravelExpenses;

namespace Quartermaster.Blazor.Pages.Forms.TravelExpenses;

public partial class TravelExpenseTotals {
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public TravelExpenseCalculationDTO Calculation { get; set; } = default!;

    private static string Money(decimal value) {
        return value.ToString("N2", German) + " €";
    }

    private static string Km(decimal value) {
        return value.ToString("0.#", German);
    }
}
