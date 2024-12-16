using Microsoft.AspNetCore.Components;
using Quartermaster.Blazor.Services;
using System.Diagnostics;

namespace Quartermaster.Blazor.Pages.DueSelector;

public partial class SelectReduced {
    [Inject]
    public required AppStateService AppState { get; set; }

    private DueSelectorEntryState? EntryState;

    private bool NextStepButtonHovered;

    protected override void OnInitialized() {
        EntryState = AppState.GetEntryState<DueSelectorEntryState>();
    }

    private static string TextForReducedTimeSpan(ReducedTimeSpan reducedTimeSpan) {
        return reducedTimeSpan switch {
            ReducedTimeSpan.OneYear => "Ich beantrage den geminderten Beitrag für ein Jahr.",
            ReducedTimeSpan.Permanent => "Ich beantrage einen dauerhaft geminderten Beitrag.",
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

        return true;
    }
}