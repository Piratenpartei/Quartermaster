using Microsoft.AspNetCore.Components;
using Quartermaster.Blazor.Services;
using System.Diagnostics;

namespace Quartermaster.Blazor.Pages.DueSelector;

public partial class SelectReduced {
    [Inject]
    public required AppStateService AppState { get; set; }

    private DueSelectorEntryState? EntryState;

    protected override void OnInitialized() {
        EntryState = AppState.GetEntryState<DueSelectorEntryState>();
    }

    private string TextForReducedTimeSpan(ReducedTimeSpan reducedTimeSpan) {
        return reducedTimeSpan switch {
            ReducedTimeSpan.OneYear => "Ich beantrage den geminderten Beitrag für ein Jahr.",
            ReducedTimeSpan.Permanent => "Ich beantrage einen dauerhaft geminderten Beitrag.",
            _ => throw new UnreachableException($"{reducedTimeSpan} is not a valid ReducedTimeSpan")
        };
    }
}