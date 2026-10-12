using System;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.AuditLog;
using Quartermaster.Rendering;

namespace Quartermaster.Server.AssemblyMotions;

public static class AssemblyMotionContent {
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

    public static AssemblyMotionDraft Snapshot(AssemblyMotion motion)
        => new(motion.TypeId, motion.Title, motion.Section, motion.TextMarkdown, motion.ReasonMarkdown,
            motion.Summary, motion.Keywords, motion.PriorDiscussion);

    public static void Apply(AssemblyMotion motion, AssemblyMotionDraft draft) {
        motion.TypeId = draft.TypeId;
        motion.Title = draft.Title;
        motion.Section = draft.Section;
        motion.TextMarkdown = draft.TextMarkdown;
        motion.TextHtml = MarkdownService.ToHtml(draft.TextMarkdown, SanitizationProfile.Standard, MarkdownFlavor.Motion);
        motion.ReasonMarkdown = draft.ReasonMarkdown;
        motion.ReasonHtml = MarkdownService.ToHtml(draft.ReasonMarkdown, SanitizationProfile.Standard, MarkdownFlavor.Motion);
        motion.Summary = draft.Summary;
        motion.Keywords = draft.Keywords;
        motion.PriorDiscussion = draft.PriorDiscussion;
    }

    public static AssemblyMotionRevision ToRevision(AssemblyMotion motion, Guid userId, DateTime nowUtc, bool editedByCommission, string? changeNote = null) => new() {
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
