using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components.AssemblyAssistant;

public partial class FlowPanel {
    private static readonly AssemblyPhase[] MotionPhases = [
        AssemblyPhase.Presentation, AssemblyPhase.Questions, AssemblyPhase.Debate, AssemblyPhase.Statement, AssemblyPhase.ClosingWord, AssemblyPhase.Vote
    ];

    private static readonly AssemblyPhase[] ElectionPhases = [
        AssemblyPhase.Candidacies, AssemblyPhase.CandidatePresentation, AssemblyPhase.CandidateQuestions, AssemblyPhase.ElectionRunning
    ];

    private static readonly AssemblyPhase[] OtherPhases = [
        AssemblyPhase.None, AssemblyPhase.GeneralDebate, AssemblyPhase.Presentation, AssemblyPhase.Questions, AssemblyPhase.Debate, AssemblyPhase.Vote
    ];

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public required AssemblySnapshotDTO Snapshot { get; set; }

    [Parameter]
    public bool CanControl { get; set; }

    [Parameter]
    public EventCallback<AssemblyPhase> OnSetPhase { get; set; }

    [Parameter]
    public EventCallback OnNextRound { get; set; }

    [Parameter]
    public EventCallback OnGoAccepted { get; set; }

    [Parameter]
    public EventCallback OnGoObjection { get; set; }

    [Parameter]
    public EventCallback OnGoEnded { get; set; }

    [Parameter]
    public EventCallback<bool> OnElectionRunning { get; set; }

    [Parameter]
    public EventCallback<bool> OnRepeatPresentation { get; set; }

    [Parameter]
    public EventCallback<string[]> OnSetCandidates { get; set; }

    [Parameter]
    public EventCallback<int> OnPresentCandidate { get; set; }

    [Parameter]
    public EventCallback<string> OnCandidateSupport { get; set; }

    private string CandidateInput = "";

    private async Task ApplyCandidates() {
        var names = CandidateInput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        await OnSetCandidates.InvokeAsync(names);
        CandidateInput = "";
    }

    private AssemblyPhase[] Phases() {
        return Snapshot.ItemKind switch {
            AssemblyItemKind.Motion => MotionPhases,
            AssemblyItemKind.Election => ElectionPhases,
            _ => OtherPhases
        };
    }

    private bool IsElection() {
        return Snapshot.ItemKind == AssemblyItemKind.Election || Array.IndexOf(ElectionPhases, Snapshot.Phase) >= 0;
    }

    private bool ShowsCounters() {
        return Snapshot.Phase == AssemblyPhase.Debate || Snapshot.Phase == AssemblyPhase.Statement;
    }

    private string Counter(string sideKey, int count) {
        return I18n[I18nParams.With(I18nKey.Ui.AssemblyConsole.SideCounter,
            ("side", I18n[sideKey]), ("count", count.ToString()), ("max", Snapshot.SpeechesPerSide.ToString()))];
    }
}
