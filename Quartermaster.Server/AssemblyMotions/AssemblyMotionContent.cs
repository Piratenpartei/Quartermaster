using System;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.AuditLog;
using Quartermaster.Rendering;

namespace Quartermaster.Server.AssemblyMotions;

public static class AssemblyMotionContent {
    public const int TitleMaxLength = 500;
    public const int SectionMaxLength = 500;
    public const int KeywordsMaxLength = 500;
    public const int SummaryMaxLength = 2000;
    public const int PriorDiscussionMaxLength = 2000;
    public const int CheckNoteMaxLength = 4000;
    public const int ChangeNoteMaxLength = 2000;

    /// <summary>
    /// Returns the refusal for a normalized draft whose single-line fields are empty or too long after normalisation, or null when they may be stored.
    /// </summary>
    public static AssemblyMotionOutcome? FieldProblem(AssemblyMotionDraft draft) {
        if (draft.Title.Length == 0)
            return AssemblyMotionOutcome.Fail(422, I18nKey.Error.AssemblyMotion.TitleRequired);
        if (draft.Title.Length > TitleMaxLength)
            return AssemblyMotionOutcome.Fail(422, I18nKey.Error.AssemblyMotion.TitleMaxLength);
        if (draft.Section?.Length > SectionMaxLength)
            return AssemblyMotionOutcome.Fail(422, I18nKey.Error.AssemblyMotion.SectionMaxLength);
        if (draft.Summary?.Length > SummaryMaxLength)
            return AssemblyMotionOutcome.Fail(422, I18nKey.Error.AssemblyMotion.SummaryMaxLength);
        if (draft.Keywords?.Length > KeywordsMaxLength)
            return AssemblyMotionOutcome.Fail(422, I18nKey.Error.AssemblyMotion.KeywordsMaxLength);
        if (draft.PriorDiscussion?.Length > PriorDiscussionMaxLength)
            return AssemblyMotionOutcome.Fail(422, I18nKey.Error.AssemblyMotion.PriorDiscussionMaxLength);

        return null;
    }

    /// <summary>
    /// Removes control characters from a single-line setting (type name, wiki prefix, assembly label); null stays null. The settings validators check this cleaned value and the endpoints store it.
    /// </summary>
    public static string? SingleLineSetting(string? value) {
        return value == null ? null : TextNormalization.RemoveControlCharacters(value);
    }

    /// <summary>
    /// Strips control characters and surrounding whitespace from a single-line note; null when nothing is left.
    /// </summary>
    public static string? NormalizeNote(string? note) {
        var cleaned = TextNormalization.RemoveControlCharacters(TextNormalization.ToNfc(note)).Trim();
        return cleaned.Length == 0 ? null : cleaned;
    }

    /// <summary>
    /// Returns the refusal for a normalized draft whose text or reason is not acceptable motion Markdown, or null when both may be stored.
    /// </summary>
    public static AssemblyMotionOutcome? MarkdownProblem(AssemblyMotionDraft draft) {
        var key = MarkdownProblemKey(draft.TextMarkdown) ?? MarkdownProblemKey(draft.ReasonMarkdown);
        return key == null ? null : AssemblyMotionOutcome.Fail(422, key);
    }

    /// <summary>
    /// Returns the message key of the first problem of a motion Markdown text, or null when the text may be stored.
    /// </summary>
    public static string? MarkdownProblemKey(string markdown) {
        var problems = MotionMarkdown.FindProblems(markdown);
        return problems.Count == 0 ? null : MotionMarkdown.MessageKey(problems[0]);
    }

    public static bool HasChange(AssemblyMotion motion, AssemblyMotionDraft draft) {
        return motion.TypeId != draft.TypeId
            || motion.Title != draft.Title
            || motion.Section != draft.Section
            || motion.TextMarkdown != draft.TextMarkdown
            || motion.ReasonMarkdown != draft.ReasonMarkdown
            || motion.Summary != draft.Summary
            || motion.Keywords != draft.Keywords
            || motion.PriorDiscussion != draft.PriorDiscussion;
    }

    /// <summary>
    /// True when the draft changes what co-submitters back: the type, the section, the text or the reason. Title, summary, keywords
    /// and prior discussion only describe the motion.
    /// </summary>
    public static bool HasBindingChange(AssemblyMotion motion, AssemblyMotionDraft draft) {
        return motion.TypeId != draft.TypeId
            || motion.Section != draft.Section
            || motion.TextMarkdown != draft.TextMarkdown
            || motion.ReasonMarkdown != draft.ReasonMarkdown;
    }

    public static AssemblyMotionDraft Snapshot(AssemblyMotion motion) {
        return new(motion.TypeId, motion.Title, motion.Section, motion.TextMarkdown, motion.ReasonMarkdown,
                motion.Summary, motion.Keywords, motion.PriorDiscussion);
    }

    public static void Apply(AssemblyMotion motion, AssemblyMotionDraft draft) {
        motion.TypeId = draft.TypeId;
        motion.Title = draft.Title;
        motion.Section = draft.Section;
        motion.TextMarkdown = draft.TextMarkdown;
        motion.TextHtml = MarkdownService.ToHtml(draft.TextMarkdown, SanitizationProfile.Motion, MarkdownFlavor.Motion);
        motion.ReasonMarkdown = draft.ReasonMarkdown;
        motion.ReasonHtml = MarkdownService.ToHtml(draft.ReasonMarkdown, SanitizationProfile.Motion, MarkdownFlavor.Motion);
        motion.Summary = draft.Summary;
        motion.Keywords = draft.Keywords;
        motion.PriorDiscussion = draft.PriorDiscussion;
    }

    public static AssemblyMotionRevision ToRevision(AssemblyMotion motion, Guid userId, DateTime nowUtc, bool editedByCommission, string? changeNote = null) {
        return new() {
            MotionId = motion.Id,
            Revision = motion.Revision,
            Title = motion.Title,
            Section = motion.Section,
            TextMarkdown = motion.TextMarkdown,
            ReasonMarkdown = motion.ReasonMarkdown,
            Summary = motion.Summary,
            Keywords = motion.Keywords,
            PriorDiscussion = motion.PriorDiscussion,
            EditedByUserId = userId,
            EditedAt = nowUtc,
            EditedByCommission = editedByCommission,
            ChangeNote = changeNote
        };
    }

    public static void LogChanges(AuditLogRepository auditLog, Guid motionId, AssemblyMotionDraft before, AssemblyMotionDraft after) {
        auditLog.LogFieldChange("AssemblyMotion", motionId, nameof(AssemblyMotion.TypeId), before.TypeId.ToString(), after.TypeId.ToString());
        auditLog.LogFieldChange("AssemblyMotion", motionId, nameof(AssemblyMotion.Title), before.Title, after.Title);
        auditLog.LogFieldChange("AssemblyMotion", motionId, nameof(AssemblyMotion.Section), before.Section, after.Section);
        auditLog.LogFieldChange("AssemblyMotion", motionId, nameof(AssemblyMotion.TextMarkdown), before.TextMarkdown, after.TextMarkdown);
        auditLog.LogFieldChange("AssemblyMotion", motionId, nameof(AssemblyMotion.ReasonMarkdown), before.ReasonMarkdown, after.ReasonMarkdown);
        auditLog.LogFieldChange("AssemblyMotion", motionId, nameof(AssemblyMotion.Summary), before.Summary, after.Summary);
        auditLog.LogFieldChange("AssemblyMotion", motionId, nameof(AssemblyMotion.Keywords), before.Keywords, after.Keywords);
        auditLog.LogFieldChange("AssemblyMotion", motionId, nameof(AssemblyMotion.PriorDiscussion), before.PriorDiscussion, after.PriorDiscussion);
    }
}
