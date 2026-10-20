using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.AuditLog;
using Quartermaster.Server.AssemblyMotions.Notifications;

namespace Quartermaster.Server.AssemblyMotions;

public class AssemblyMotionCommissionService {
    private readonly AssemblyMotionRepository _motions;
    private readonly AuditLogRepository _auditLog;
    private readonly AssemblyMotionNotifier _notifier;

    public AssemblyMotionCommissionService(AssemblyMotionRepository motions, AuditLogRepository auditLog, AssemblyMotionNotifier notifier) {
        _motions = motions;
        _auditLog = auditLog;
        _notifier = notifier;
    }

    public AssemblyMotionOutcome Check(Guid motionId, AssemblyMotionStatus decision, string? checkNote, Guid userId, DateTime nowUtc) {
        var note = AssemblyMotionContent.NormalizeNote(checkNote);
        if (!IsCheckStatus(decision))
            return AssemblyMotionOutcome.Fail(422, I18nKey.Error.AssemblyMotion.InvalidState);
        if (note?.Length > AssemblyMotionContent.CheckNoteMaxLength)
            return AssemblyMotionOutcome.Fail(422, I18nKey.Error.AssemblyMotion.CheckNoteMaxLength);

        using var tx = _motions.BeginTransaction();

        var motion = _motions.GetForUpdate(motionId);
        if (motion == null)
            return AssemblyMotionOutcome.Fail(404, I18nKey.Error.AssemblyMotion.NotFound);
        if (!IsCheckStatus(motion.Status))
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.InvalidState);
        if (decision == AssemblyMotionStatus.FormallyInsufficient && note == null)
            return AssemblyMotionOutcome.Fail(422, I18nKey.Error.AssemblyMotion.CheckNoteRequired);

        var oldStatus = motion.Status;
        var oldNote = motion.CheckNote;

        motion.Status = decision;
        motion.CheckNote = note;
        motion.CheckedAt = nowUtc;
        motion.CheckedByUserId = userId;
        _motions.Update(motion);
        _motions.MarkMirrorsPending(motion.Id, nowUtc);

        _auditLog.LogFieldChange("AssemblyMotion", motion.Id, nameof(AssemblyMotion.Status), oldStatus.ToString(), decision.ToString());
        _auditLog.LogFieldChange("AssemblyMotion", motion.Id, nameof(AssemblyMotion.CheckNote), oldNote, note);

        var submitterIds = SubmitterIds(motion.Id);

        tx.Commit();
        _notifier.Checked(motion, submitterIds);
        return AssemblyMotionOutcome.Ok(motion);
    }

    public AssemblyMotionOutcome Edit(Guid motionId, Guid userId, AssemblyMotionDraft draft, int baseRevision, string? changeNote, DateTime nowUtc) {
        draft = draft.Normalized();
        var contentProblem = AssemblyMotionContent.FieldProblem(draft) ?? AssemblyMotionContent.MarkdownProblem(draft);
        if (contentProblem != null)
            return contentProblem;

        var note = AssemblyMotionContent.NormalizeNote(changeNote);
        if (note?.Length > AssemblyMotionContent.ChangeNoteMaxLength)
            return AssemblyMotionOutcome.Fail(422, I18nKey.Error.AssemblyMotion.ChangeNoteMaxLength);

        using var tx = _motions.BeginTransaction();

        var motion = _motions.GetForUpdate(motionId);
        if (motion == null)
            return AssemblyMotionOutcome.Fail(404, I18nKey.Error.AssemblyMotion.NotFound);
        if (motion.Number == null || !IsCheckStatus(motion.Status))
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.InvalidState);
        if (note == null)
            return AssemblyMotionOutcome.Fail(422, I18nKey.Error.AssemblyMotion.ChangeNoteRequired);
        if (baseRevision != motion.Revision)
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.RevisionConflict);
        if (draft.TypeId != motion.TypeId)
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.InvalidState);

        if (!AssemblyMotionContent.HasChange(motion, draft))
            return AssemblyMotionOutcome.Ok(motion);

        var before = AssemblyMotionContent.Snapshot(motion);
        AssemblyMotionContent.Apply(motion, draft);
        motion.Revision += 1;
        _motions.Update(motion);
        _motions.MarkMirrorsPending(motion.Id, nowUtc);

        _motions.AddRevision(AssemblyMotionContent.ToRevision(motion, userId, nowUtc, true, note));

        AssemblyMotionContent.LogChanges(_auditLog, motion.Id, before, draft);

        var submitterIds = SubmitterIds(motion.Id);

        tx.Commit();
        _notifier.EditedByCommission(motion, note, submitterIds);
        return AssemblyMotionOutcome.Ok(motion);
    }

    public AssemblyMotionOutcome Withdraw(Guid motionId, Guid userId, DateTime nowUtc) {
        using var tx = _motions.BeginTransaction();

        var motion = _motions.GetForUpdate(motionId);
        if (motion == null)
            return AssemblyMotionOutcome.Fail(404, I18nKey.Error.AssemblyMotion.NotFound);
        if (motion.Status == AssemblyMotionStatus.Draft || motion.Status == AssemblyMotionStatus.Withdrawn || motion.Status == AssemblyMotionStatus.Lapsed)
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.InvalidState);

        motion.Status = AssemblyMotionStatus.Withdrawn;
        motion.WithdrawnAt = nowUtc;
        _motions.Update(motion);
        _motions.MarkMirrorsPending(motion.Id, nowUtc);

        _auditLog.Log("AssemblyMotion", motion.Id, "Withdrawn", "ByCommission", null, "true");

        var submitterIds = SubmitterIds(motion.Id);

        tx.Commit();
        _notifier.Withdrawn(motion, submitterIds, false);
        return AssemblyMotionOutcome.Ok(motion);
    }

    private List<Guid> SubmitterIds(Guid motionId) {
        return _motions.GetSubmitters(motionId)
                .Select(s => s.MemberId)
                .ToList();
    }

    private static bool IsCheckStatus(AssemblyMotionStatus status) {
        if (status == AssemblyMotionStatus.Submitted)
            return true;
        if (status == AssemblyMotionStatus.Admitted)
            return true;
        return status == AssemblyMotionStatus.FormallyInsufficient;
    }
}
