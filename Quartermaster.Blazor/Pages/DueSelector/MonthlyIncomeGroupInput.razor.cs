using System.Globalization;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Pages.DueSelector;

public partial class MonthlyIncomeGroupInput {
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public DueSelectorEntryState State { get; set; } = default!;
}
