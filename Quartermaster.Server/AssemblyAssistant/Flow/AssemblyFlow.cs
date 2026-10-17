using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Quartermaster.Api.AssemblyAssistant;

namespace Quartermaster.Server.AssemblyAssistant.Flow;

/// <summary>
/// Phase and counters of the current agenda item (GO BPT §§ 11 to 13, 15): up to five Pro and five Contra speeches per round,
/// unused slots do not pass to the other side; a GO motion interrupts the phase and the phase resumes afterwards; in a
/// candidate questioning at most five members ask.
/// </summary>
public class AssemblyFlow {
    public const int SpeechesPerSide = 5;
    public const int MaxQuestioners = 5;

    private static readonly StringComparer GermanOrder = StringComparer.Create(CultureInfo.GetCultureInfo("de-DE"), ignoreCase: true);

    private readonly List<string> _candidates = [];

    public AssemblyPhase Phase { get; private set; }
    public int Round { get; private set; }
    public int ProCount { get; private set; }
    public int ContraCount { get; private set; }
    public QueueKind? LastSide { get; private set; }
    public bool GoActive { get; private set; }
    public bool GoObjection { get; private set; }
    public AssemblyPhase? InterruptedPhase { get; private set; }
    public bool RepeatPresentation { get; set; }
    public int Questioners { get; private set; }
    public bool ElectionRunning { get; set; }
    public IReadOnlyList<string> Candidates => _candidates;
    public int? CurrentCandidate { get; private set; }

    public static bool IsElectionPhase(AssemblyPhase phase) {
        return phase == AssemblyPhase.Candidacies || phase == AssemblyPhase.CandidatePresentation
            || phase == AssemblyPhase.CandidateQuestions || phase == AssemblyPhase.ElectionRunning;
    }

    public void Reset(AssemblyItemKind kind) {
        Phase = kind switch {
            AssemblyItemKind.Motion => AssemblyPhase.Presentation,
            AssemblyItemKind.Election => AssemblyPhase.CandidatePresentation,
            AssemblyItemKind.GeneralDebate => AssemblyPhase.GeneralDebate,
            _ => AssemblyPhase.None
        };
        Round = 0;
        ResetCounters();
        GoActive = false;
        GoObjection = false;
        InterruptedPhase = null;
        RepeatPresentation = false;
        Questioners = 0;
        ElectionRunning = false;
        _candidates.Clear();
        CurrentCandidate = null;
    }

    /// <summary>Candidates present in alphabetical order of their surname, taken as the last word of the name (GO § 11).</summary>
    public void SetCandidates(IEnumerable<string> names) {
        var ordered = names
            .Select(n => n.Trim())
            .Where(n => n.Length > 0)
            .Distinct()
            .OrderBy(Surname, GermanOrder)
            .ThenBy(n => n, GermanOrder)
            .ToList();

        _candidates.Clear();
        _candidates.AddRange(ordered);
        CurrentCandidate = null;
    }

    public bool SetCurrentCandidate(int? index) {
        if (index != null && (index < 0 || index >= _candidates.Count))
            return false;

        CurrentCandidate = index;
        return true;
    }

    public void SetPhase(AssemblyPhase phase) {
        if (phase == AssemblyPhase.Debate && Round == 0) {
            Round = 1;
            ResetCounters();
        }
        if (phase == AssemblyPhase.CandidateQuestions && Phase != AssemblyPhase.CandidateQuestions)
            Questioners = 0;

        Phase = phase;
    }

    /// <summary>The assembly decided to continue the debate: the motion author gets the statement, then round n+1 follows (GO § 13 (3), (5)).</summary>
    public void StartNextRound() {
        Round = (Round == 0 ? 1 : Round) + 1;
        ResetCounters();
        Phase = AssemblyPhase.Statement;
    }

    public void OnFloorArrival(SpeakerRole role) {
        if (role == SpeakerRole.Point && !GoActive) {
            GoActive = true;
            InterruptedPhase = Phase;
            return;
        }
        // The objection to a GO motion is no Pro or Contra speech of the debate.
        if (GoActive)
            return;

        if (Phase == AssemblyPhase.Debate && role == SpeakerRole.Pro) {
            ProCount++;
            LastSide = QueueKind.Pro;
        } else if (Phase == AssemblyPhase.Debate && role == SpeakerRole.Contra) {
            ContraCount++;
            LastSide = QueueKind.Contra;
        } else if (Phase == AssemblyPhase.CandidateQuestions && role == SpeakerRole.Question) {
            Questioners++;
        }
    }

    public void StartGoObjection() {
        GoObjection = true;
    }

    /// <summary>Ends the GO motion, accepted without objection or after its poll, and returns to the interrupted phase.</summary>
    public void EndGo() {
        GoActive = false;
        GoObjection = false;
        if (InterruptedPhase != null)
            Phase = InterruptedPhase.Value;
        InterruptedPhase = null;
    }

    public bool HasSlot(QueueKind side) {
        return side == QueueKind.Pro ? ProCount < SpeechesPerSide : ContraCount < SpeechesPerSide;
    }

    private static string Surname(string name) {
        return name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Last();
    }

    private void ResetCounters() {
        ProCount = 0;
        ContraCount = 0;
        LastSide = null;
    }
}
