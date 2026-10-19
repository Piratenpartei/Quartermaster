using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.TravelExpenses;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Forms.TravelExpenses;

public partial class TravelExpenseChapterStep {
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    [Inject]
    public required I18nService I18n { get; set; }
    [Inject]
    public required ChapterNames ChapterNames { get; set; }

    [Parameter, EditorRequired]
    public TravelExpenseInputDTO Input { get; set; } = default!;

    [Parameter, EditorRequired]
    public TravelExpenseRulesDTO Rules { get; set; } = default!;

    /// <summary>The chapter decides the rules, so the page reloads them.</summary>
    [Parameter]
    public EventCallback OnChanged { get; set; }

    private string[] FunctionSuggestions() {
        return I18n[I18nKey.Ui.TravelExpense.FunctionSuggestions].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private async Task OnChapterPicked(string raw) {
        Input.TravelChapterId = Guid.TryParse(raw, out var id) ? id : null;
        Input.TravelChapterName = Input.TravelChapterId == null ? "" : await ChapterNames.NameOfAsync(Input.TravelChapterId.Value);
        await OnChanged.InvokeAsync();
    }

    private string RateText(IReadOnlyList<RateStepDTO> steps) {
        if (steps.Count == 1)
            return Money(steps[0].RatePerKm);

        return string.Join(", ", steps.Select(s => s.UpToKm == null
            ? I18n[I18nParams.With(I18nKey.Ui.TravelExpense.Pdf.RateThen, ("rate", Money(s.RatePerKm)))]
            : I18n[I18nParams.With(I18nKey.Ui.TravelExpense.Pdf.RateUpTo, ("rate", Money(s.RatePerKm)), ("km", s.UpToKm.Value.ToString("0.##", German)))]));
    }

    private static string Money(decimal value) {
        return value.ToString("N2", German) + " €";
    }
}
