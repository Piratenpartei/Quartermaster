using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.I18n;
using Quartermaster.Server.AssemblyAssistant.OpenSlides;
using Quartermaster.Server.AssemblyMotions.OpenSlides;

namespace Quartermaster.Server.AssemblyAssistant.Templates;

public record PollTemplateRequest(PollTemplate Template, string? Text, int? Threshold);

/// <summary>A motion that competes with the current one, by OpenSlides motion id.</summary>
public record CompetingMotion(int OpenSlidesMotionId, string Label);

public record PollPlanContext(int MeetingId, int? DelegatesGroupId, OpenSlidesAgendaItemView? CurrentItem, OpenSlidesMeetingView View,
    IReadOnlyList<CompetingMotion> Competitors, string CurrentMotionLabel);

/// <summary>
/// Where and how a template poll is created. <see cref="ContentObjectId"/> is set when the target exists; otherwise the host
/// first creates the procedural topic <see cref="NewTopicTitle"/> below <see cref="NewTopicParentId"/>. A null
/// <see cref="Kind"/> means the evaluation follows the motion type (final vote).
/// </summary>
public record PollPlan(string Title, PollEvaluationKind? Kind, int Threshold, string? ContentObjectId, string? NewTopicTitle, int? NewTopicParentId,
    Func<string, OpenSlidesPollCreate> Build);

/// <summary>
/// The standard polls of the spec. Yes/No polls on topics carry the two options "Ja" and "Nein" (a single-option topic poll
/// fails in the OpenSlides client) with one vote and no abstain option: abstaining means not voting. The final vote is a
/// motion poll with Ja/Nein/Enthaltung. Competing motions are an acceptance vote (GO § 7): one vote per option, and the
/// global "No" as the valid empty ballot that rejects all. Titles carry the motion code so the minutes can be rebuilt from
/// OpenSlides alone.
/// </summary>
public static class AssemblyPollTemplates {
    public const int SupportThreshold = 20;
    public const int ControversyThreshold = 10;
    public const int MaxThreshold = 10_000;

    public static (PollPlan? Plan, string? ErrorKey) Plan(PollTemplateRequest request, PollPlanContext context) {
        var item = context.CurrentItem;
        if (item == null)
            return (null, I18nKey.Ui.AssemblyConsole.Reject.NoAgendaItem);
        if (context.DelegatesGroupId == null)
            return (null, I18nKey.Ui.AssemblyConsole.Reject.DelegatesGroupMissing);
        if (NeedsText(request.Template) && string.IsNullOrWhiteSpace(request.Text))
            return (null, I18nKey.Ui.AssemblyConsole.Reject.TextRequired);
        if (request.Threshold != null && (request.Threshold < 1 || request.Threshold > MaxThreshold))
            return (null, I18nKey.Ui.AssemblyConsole.Reject.InvalidThreshold);

        var isMotion = item.ContentObjectId.StartsWith("motion/");
        var code = item.MotionNumber ?? item.Title;
        var text = request.Text?.Trim() ?? "";
        var group = context.DelegatesGroupId.Value;

        return request.Template switch {
            PollTemplate.FinalVote => isMotion
                ? (FinalVote($"Schlussabstimmung {code}", item.ContentObjectId, context.MeetingId, group), null)
                : (null, I18nKey.Ui.AssemblyConsole.Reject.NotAMotion),
            PollTemplate.Competition => context.Competitors.Count == 0
                ? (null, I18nKey.Ui.AssemblyConsole.Reject.NoCompetitors)
                : (Procedural(context, item, Acceptance(context, code, false)), null),
            PollTemplate.Runoff => context.Competitors.Count == 0
                ? (null, I18nKey.Ui.AssemblyConsole.Reject.NoCompetitors)
                : (Procedural(context, item, Acceptance(context, code, true)), null),
            _ => (Procedural(context, item, YesNo(request, code, text)), null)
        };
    }

    private static bool NeedsText(PollTemplate template) {
        return template == PollTemplate.GoMotion || template == PollTemplate.Opinion || template == PollTemplate.Support;
    }

    private static TemplatePoll YesNo(PollTemplateRequest request, string code, string text) {
        var (title, kind, threshold) = request.Template switch {
            PollTemplate.ContinueDebate => ($"{code}: Debatte fortsetzen?", PollEvaluationKind.Simple, 0),
            PollTemplate.GoMotion => ($"{code}: GO-Antrag: {text}", PollEvaluationKind.Simple, 0),
            PollTemplate.Opinion => ($"{code}: Meinungsbild: {text}", PollEvaluationKind.NumbersOnly, 0),
            PollTemplate.Support => ($"{code}: Unterstützung {text}", PollEvaluationKind.Threshold, request.Threshold ?? SupportThreshold),
            PollTemplate.Controversy => ($"{code}: Kontroverse {text}".TrimEnd(), PollEvaluationKind.Threshold, ControversyThreshold),
            PollTemplate.QuestionRound => ($"{code}: Weitere Fragerunde?", PollEvaluationKind.Simple, 0),
            _ => ($"{code}: Blockabstimmung", PollEvaluationKind.Simple, 0)
        };

        return new TemplatePoll(title, kind, threshold, (contentObject, meetingId, group) => new OpenSlidesPollCreate(meetingId, title, contentObject, "Y",
            [new OpenSlidesPollOption("Ja", null), new OpenSlidesPollOption("Nein", null)], group, 1, 1, 1, false, false, false, "valid"));
    }

    private static TemplatePoll Acceptance(PollPlanContext context, string code, bool runoff) {
        var options = context.Competitors
            .Select(c => c.Label)
            .Prepend(context.CurrentMotionLabel)
            .Select(label => new OpenSlidesPollOption(label, null))
            .ToList();
        var title = runoff ? $"{code}: Stichwahl konkurrierender Anträge" : $"{code}: Konkurrenz";
        var maxVotes = runoff ? 1 : options.Count;
        var kind = runoff ? PollEvaluationKind.NumbersOnly : PollEvaluationKind.Acceptance;

        return new TemplatePoll(title, kind, 0, (contentObject, meetingId, group) => new OpenSlidesPollCreate(meetingId, title, contentObject, "Y",
            options, group, 0, maxVotes, 1, false, true, false, "valid"));
    }

    private static PollPlan Procedural(PollPlanContext context, OpenSlidesAgendaItemView item, TemplatePoll poll) {
        var meetingId = context.MeetingId;
        var group = context.DelegatesGroupId!.Value;
        OpenSlidesPollCreate Build(string contentObject) => poll.Build(contentObject, meetingId, group);

        if (!item.ContentObjectId.StartsWith("motion/"))
            return new PollPlan(poll.Title, poll.Kind, poll.Threshold, item.ContentObjectId, null, null, Build);

        var existing = ProceduralTopics.Find(context.View, item);
        return existing != null
            ? new PollPlan(poll.Title, poll.Kind, poll.Threshold, existing, null, null, Build)
            : new PollPlan(poll.Title, poll.Kind, poll.Threshold, null, ProceduralTopics.TitleFor(item), item.Id, Build);
    }

    // The evaluation of a final vote follows the motion type's majority, which the poll state derives from the motion id.
    private static PollPlan FinalVote(string title, string motionObject, int meetingId, int group) {
        return new PollPlan(title, null, 0, motionObject, null, null, contentObject => new OpenSlidesPollCreate(meetingId, title,
            contentObject, "YNA", [new OpenSlidesPollOption(null, contentObject)], group, 1, 1, 1, false, false, false, "YN"));
    }

    private record TemplatePoll(string Title, PollEvaluationKind Kind, int Threshold, Func<string, int, int, OpenSlidesPollCreate> Build);
}
