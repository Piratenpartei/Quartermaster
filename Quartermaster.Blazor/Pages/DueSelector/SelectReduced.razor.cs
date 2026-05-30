using System.Diagnostics;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.DueSelector;

public partial class SelectReduced {
    [Inject]
    public required AppStateService AppState { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }

    private DueSelectorEntryState? EntryState;

    private bool NextStepButtonHovered;

    protected override void OnInitialized() {
        EntryState = AppState.GetEntryState<DueSelectorEntryState>();
    }

    private string TextForReducedTimeSpan(ReducedTimeSpan reducedTimeSpan) {
        return reducedTimeSpan switch {
            ReducedTimeSpan.OneYear => I18n[I18nKey.Ui.SelectReduced.TimeSpanOneYear],
            ReducedTimeSpan.Permanent => I18n[I18nKey.Ui.SelectReduced.TimeSpanPermanent],
            _ => throw new UnreachableException($"{reducedTimeSpan} is not a valid ReducedTimeSpan")
        };
    }

    private bool CanContinue() {
        if (EntryState == null)
            return true;

        if (string.IsNullOrEmpty(EntryState.ReducedJustification))
            return false;
        if (EntryState.ReducedAmount < 1)
            return false;

        EntryState.MonthlyIncomeGroup = 0;
        return true;
    }
}
