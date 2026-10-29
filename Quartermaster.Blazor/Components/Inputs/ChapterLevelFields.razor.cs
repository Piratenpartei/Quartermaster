using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components.Inputs;

public partial class ChapterLevelFields {
    private static readonly ChapterLevel[] SelectableLevels = [
        ChapterLevel.State,
        ChapterLevel.District,
        ChapterLevel.County,
        ChapterLevel.Local
    ];

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter]
    public string IdPrefix { get; set; } = "chapter";

    [Parameter]
    public bool IsRoot { get; set; }

    [Parameter]
    public ChapterLevel? Level { get; set; }

    [Parameter]
    public EventCallback<ChapterLevel?> LevelChanged { get; set; }

    [Parameter]
    public int? PeriodDays { get; set; }

    [Parameter]
    public EventCallback<int?> PeriodDaysChanged { get; set; }

    private string LevelValue => Level.HasValue ? ((int)Level.Value).ToString() : "";

    private string DefaultPlaceholder => DefaultDays(IsRoot, Level)?.ToString() ?? "";

    internal static int? DefaultDays(bool isRoot, ChapterLevel? level) {
        if (isRoot)
            return ChapterTakeOverPeriods.FederalDefaultDays;
        if (level == ChapterLevel.State)
            return ChapterTakeOverPeriods.StateDefaultDays;
        return null;
    }

    internal static string LevelKey(ChapterLevel level) => level switch {
        ChapterLevel.Federal => I18nKey.Ui.HandUp.LevelFederal,
        ChapterLevel.State => I18nKey.Ui.HandUp.LevelState,
        ChapterLevel.District => I18nKey.Ui.HandUp.LevelDistrict,
        ChapterLevel.County => I18nKey.Ui.HandUp.LevelCounty,
        _ => I18nKey.Ui.HandUp.LevelLocal
    };

    private async Task OnLevelChanged(ChangeEventArgs args) {
        var parsed = int.TryParse(args.Value?.ToString(), out var value) ? (ChapterLevel?)value : null;
        await LevelChanged.InvokeAsync(parsed);
    }

    private async Task OnPeriodChanged(ChangeEventArgs args) {
        var parsed = int.TryParse(args.Value?.ToString(), out var value) ? (int?)value : null;
        await PeriodDaysChanged.InvokeAsync(parsed);
    }
}
