using System.Collections.Generic;
using System.Linq;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Server.AssemblyAssistant.Session;

namespace Quartermaster.Server.AssemblyAssistant.Flow;

/// <summary>
/// Who should get the floor next. GO motions rank highest and questions come before Pro and Contra (GO § 12 (3), § 15 (4)),
/// but both only while nobody speaks and no poll runs, since neither may interrupt. In the debate Pro and Contra alternate,
/// starting with Pro, while both sides have requests and free slots.
/// </summary>
public static class SuggestionEngine {
    public static QueueEntry? Suggest(AssemblyFlow flow, IReadOnlyDictionary<QueueKind, SpeakerQueue> queues, bool busy) {
        if (flow.GoActive)
            return null;

        var point = busy ? null : queues[QueueKind.Point].FirstSuggestable();
        if (point != null)
            return point;

        var question = busy ? null : queues[QueueKind.Question].FirstSuggestable();
        return flow.Phase switch {
            AssemblyPhase.Questions => question,
            AssemblyPhase.CandidateQuestions => flow.Questioners < AssemblyFlow.MaxQuestioners ? question : null,
            AssemblyPhase.Debate => question ?? Alternation(flow, queues),
            AssemblyPhase.GeneralDebate => EarliestOf(queues, QueueKind.Question, QueueKind.Pro, QueueKind.Contra),
            AssemblyPhase.None => question ?? queues[QueueKind.Pro].FirstSuggestable() ?? queues[QueueKind.Contra].FirstSuggestable(),
            _ => null
        };
    }

    /// <summary>The round is over when no side has both a free slot and a waiting request.</summary>
    public static bool IsRoundComplete(AssemblyFlow flow, IReadOnlyDictionary<QueueKind, SpeakerQueue> queues) {
        return flow.Phase == AssemblyPhase.Debate && Alternation(flow, queues) == null;
    }

    public static List<string> NextNames(SpeakerQueue queue, int count) {
        return queue.Entries
            .Where(e => e.IsSuggestable())
            .Take(count)
            .Select(e => e.Identity.Name)
            .ToList();
    }

    private static QueueEntry? Alternation(AssemblyFlow flow, IReadOnlyDictionary<QueueKind, SpeakerQueue> queues) {
        var pro = flow.HasSlot(QueueKind.Pro) ? queues[QueueKind.Pro].FirstSuggestable() : null;
        var contra = flow.HasSlot(QueueKind.Contra) ? queues[QueueKind.Contra].FirstSuggestable() : null;
        if (pro != null && contra != null)
            return flow.LastSide == QueueKind.Pro ? contra : pro;

        return pro ?? contra;
    }

    private static QueueEntry? EarliestOf(IReadOnlyDictionary<QueueKind, SpeakerQueue> queues, params QueueKind[] kinds) {
        return kinds
            .Select(k => queues[k].FirstSuggestable())
            .Where(e => e != null)
            .OrderBy(e => e!.EnteredAt)
            .ThenBy(e => e!.Sequence)
            .FirstOrDefault();
    }
}
