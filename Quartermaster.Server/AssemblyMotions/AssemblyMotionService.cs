using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Data;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Members;
using Quartermaster.Rendering;
using Quartermaster.Server.AssemblyMotions.Notifications;

namespace Quartermaster.Server.AssemblyMotions;

public class AssemblyMotionService {
    public const int MaxDraftsPerMemberAndPortal = 50;

    private static readonly TimeSpan CollectingReminderLead = TimeSpan.FromHours(48);

    private readonly AssemblyMotionRepository _motions;
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly AuditLogRepository _auditLog;
    private readonly DbContext _db;
    private readonly AssemblyMotionNotifier _notifier;

    public AssemblyMotionService(AssemblyMotionRepository motions, AssemblyMotionPortalRepository portals,
        AuditLogRepository auditLog, DbContext db, AssemblyMotionNotifier notifier) {
        _motions = motions;
        _portals = portals;
        _auditLog = auditLog;
        _db = db;
        _notifier = notifier;
    }

    public AssemblyMotionOutcome CreateDraft(AssemblyMotionPortal portal, Member author, Guid userId, AssemblyMotionDraft draft, DateTime nowUtc) {
        draft = draft.Normalized();
        var contentProblem = AssemblyMotionContent.FieldProblem(draft) ?? AssemblyMotionContent.MarkdownProblem(draft);
        if (contentProblem != null)
            return contentProblem;
        if (nowUtc < portal.OpensAt)
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.PortalNotOpen);
        if (nowUtc > portal.ClosesAt)
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.PortalClosed);

        var type = _portals.GetType(draft.TypeId);
        if (type == null || type.PortalId != portal.Id)
            return AssemblyMotionOutcome.Fail(422, I18nKey.Error.AssemblyMotion.TypeNotInPortal);

        using var tx = _motions.BeginTransaction();

        // Not serialized per member: parallel requests of one member can exceed the cap by the number of requests in flight.
        if (_motions.CountDrafts(portal.Id, author.Id) >= MaxDraftsPerMemberAndPortal)
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.DraftLimitReached);

        var motion = new AssemblyMotion {
            PortalId = portal.Id,
            TypeId = draft.TypeId,
            Title = draft.Title,
            Section = draft.Section,
            TextMarkdown = draft.TextMarkdown,
            TextHtml = MarkdownService.ToHtml(draft.TextMarkdown, SanitizationProfile.Motion, MarkdownFlavor.Motion),
            ReasonMarkdown = draft.ReasonMarkdown,
            ReasonHtml = MarkdownService.ToHtml(draft.ReasonMarkdown, SanitizationProfile.Motion, MarkdownFlavor.Motion),
            Summary = draft.Summary,
            Keywords = draft.Keywords,
            PriorDiscussion = draft.PriorDiscussion,
            Status = AssemblyMotionStatus.Draft,
            Revision = 1,
            CreatedByMemberId = author.Id,
            CreatedAt = nowUtc
        };
        _motions.Insert(motion);

        _motions.AddSubmitter(new AssemblyMotionSubmitter {
            MotionId = motion.Id,
            MemberId = author.Id,
            IsAuthor = true,
            JoinedAt = nowUtc,
            JoinedRevision = 1
        });

        _motions.AddRevision(new AssemblyMotionRevision {
            MotionId = motion.Id,
            Revision = 1,
            Title = motion.Title,
            Section = motion.Section,
            TextMarkdown = motion.TextMarkdown,
            ReasonMarkdown = motion.ReasonMarkdown,
            Summary = motion.Summary,
            Keywords = motion.Keywords,
            PriorDiscussion = motion.PriorDiscussion,
            EditedByUserId = userId,
            EditedAt = nowUtc,
            EditedByCommission = false
        });

        _auditLog.LogCreated("AssemblyMotion", motion.Id);

        tx.Commit();
        return AssemblyMotionOutcome.Ok(motion);
    }

    public AssemblyMotionOutcome UpdateByAuthor(AssemblyMotion motion, Member author, Guid userId, AssemblyMotionDraft draft, int baseRevision, DateTime nowUtc) {
        draft = draft.Normalized();
        var contentProblem = AssemblyMotionContent.FieldProblem(draft) ?? AssemblyMotionContent.MarkdownProblem(draft);
        if (contentProblem != null)
            return contentProblem;

        using var tx = _motions.BeginTransaction();

        var notifications = new List<Action>();
        var locked = _motions.GetForUpdate(motion.Id);
        if (locked == null)
            return AssemblyMotionOutcome.Fail(404, I18nKey.Error.AssemblyMotion.NotFound);

        var portal = _portals.Get(locked.PortalId)
            ?? throw new InvalidOperationException($"Portal {locked.PortalId} not found for motion {locked.Id}.");
        var currentType = _portals.GetType(locked.TypeId)
            ?? throw new InvalidOperationException($"Type {locked.TypeId} not found for motion {locked.Id}.");

        var problem = EditProblem(portal, currentType, locked, author.Id, nowUtc, locked.IsCompetingMotion);
        if (problem != null)
            return problem;
        if (baseRevision != locked.Revision)
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.RevisionConflict);

        var type = currentType;
        if (draft.TypeId != locked.TypeId) {
            if (locked.Number != null)
                return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.InvalidState);

            type = _portals.GetType(draft.TypeId);
            if (type == null || type.PortalId != locked.PortalId)
                return AssemblyMotionOutcome.Fail(422, I18nKey.Error.AssemblyMotion.TypeNotInPortal);

            problem = EditProblem(portal, type, locked, author.Id, nowUtc, locked.IsCompetingMotion);
            if (problem != null)
                return problem;
        }

        if (!AssemblyMotionContent.HasChange(locked, draft))
            return AssemblyMotionOutcome.Ok(locked);

        var isBindingChange = AssemblyMotionContent.HasBindingChange(locked, draft);
        var before = AssemblyMotionContent.Snapshot(locked);
        AssemblyMotionContent.Apply(locked, draft);
        locked.Revision += 1;

        var removedCoSubmitterIds = new List<Guid>();
        if (locked.Status != AssemblyMotionStatus.Draft && isBindingChange) {
            removedCoSubmitterIds = _motions.RemoveCoSubmitters(locked.Id);
            locked.CollectingReminderSentAt = null;
            if (locked.Status == AssemblyMotionStatus.Submitted) {
                if (type.MinSubmitters > 1) {
                    locked.Status = AssemblyMotionStatus.CollectingCoSubmitters;
                    locked.SubmittedAt = null;
                } else {
                    locked.SubmittedAt = nowUtc;
                }
            }
        }

        var submitsNow = locked.Status == AssemblyMotionStatus.CollectingCoSubmitters && type.MinSubmitters <= 1;
        if (submitsNow && locked.Number == null) {
            // Taken before the update below: that update puts a shared foreign-key lock on the new type row, and two authors upgrading to the same type would then deadlock when Submit asks for the exclusive lock.
            _motions.LockType(type.Id);
        }
        _motions.Update(locked);

        if (submitsNow) {
            Submit(locked, nowUtc, notifications);
            notifications.Add(() => _notifier.Submitted(locked));
        }

        _motions.MarkMirrorsPending(locked.Id, nowUtc);

        _db.AssemblyMotionSubmitters
            .Where(s => s.MotionId == locked.Id && s.IsAuthor)
            .Set(s => s.JoinedRevision, locked.Revision)
            .Update();

        _motions.AddRevision(AssemblyMotionContent.ToRevision(locked, userId, nowUtc, false));

        AssemblyMotionContent.LogChanges(_auditLog, locked.Id, before, draft);
        if (removedCoSubmitterIds.Count > 0) {
            _auditLog.Log("AssemblyMotion", locked.Id, "CoSubmittersReset", "CoSubmitterCount", removedCoSubmitterIds.Count.ToString(), "0");
            notifications.Add(() => _notifier.CoSubmittersReset(locked, removedCoSubmitterIds));
        }

        tx.Commit();
        RunAll(notifications);
        return AssemblyMotionOutcome.Ok(locked);
    }

    public bool CanEditAsAuthor(AssemblyMotion motion, AssemblyMotionType type, AssemblyMotionPortal portal, Guid? memberId, DateTime nowUtc) {
        return EditProblem(portal, type, motion, memberId, nowUtc, motion.IsCompetingMotion) == null;
    }

    /// <summary>
    /// Whether the author could edit <paramref name="motion"/> if its competing flag were <paramref name="isCompetingMotion"/>; used to decide whether the flag may be set.
    /// </summary>
    public bool CanEditAsAuthor(AssemblyMotion motion, AssemblyMotionType type, AssemblyMotionPortal portal, Guid? memberId, DateTime nowUtc, bool isCompetingMotion) {
        return EditProblem(portal, type, motion, memberId, nowUtc, isCompetingMotion) == null;
    }

    private AssemblyMotionOutcome? EditProblem(AssemblyMotionPortal portal, AssemblyMotionType type, AssemblyMotion motion, Guid? memberId, DateTime nowUtc, bool isCompetingMotion) {
        if (memberId == null || motion.CreatedByMemberId != memberId)
            return AssemblyMotionOutcome.Fail(403, I18nKey.Error.AssemblyMotion.NotAuthor);
        if (!IsAuthorEditableStatus(motion.Status))
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.EditLocked);

        var effectiveDeadline = AssemblyMotionDeadlines.Effective(portal, type, isCompetingMotion);
        if (AssemblyMotionDeadlines.IsPast(effectiveDeadline, nowUtc))
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.EditLocked);

        return null;
    }

    private static bool IsAuthorEditableStatus(AssemblyMotionStatus status) {
        if (status == AssemblyMotionStatus.Draft)
            return true;
        if (status == AssemblyMotionStatus.CollectingCoSubmitters)
            return true;
        return status == AssemblyMotionStatus.Submitted;
    }

    public bool CanRelease(AssemblyMotionPortal portal, AssemblyMotionType type, AssemblyMotion motion, Member author, DateTime nowUtc) {
        return ReleaseProblem(portal, type, motion, author, nowUtc) == null;
    }

    private AssemblyMotionOutcome? ReleaseProblem(AssemblyMotionPortal portal, AssemblyMotionType type, AssemblyMotion motion, Member author, DateTime nowUtc) {
        if (motion.CreatedByMemberId != author.Id)
            return AssemblyMotionOutcome.Fail(403, I18nKey.Error.AssemblyMotion.NotAuthor);
        if (motion.Status != AssemblyMotionStatus.Draft)
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.InvalidState);
        if (nowUtc < portal.OpensAt)
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.PortalNotOpen);

        var effectiveDeadline = AssemblyMotionDeadlines.Effective(portal, type, motion.IsCompetingMotion);
        if (AssemblyMotionDeadlines.IsPast(effectiveDeadline, nowUtc)) {
            return AssemblyMotionOutcome.Fail(409, I18nParams.With(I18nKey.Error.AssemblyMotion.DeadlinePassed,
                ("deadline", BerlinTime.FormatDeadline(effectiveDeadline))));
        }

        return null;
    }

    public AssemblyMotionOutcome Release(Guid motionId, Member author, DateTime nowUtc) {
        var notifications = new List<Action>();
        using var tx = _motions.BeginTransaction();

        var motion = _motions.GetForUpdate(motionId);
        if (motion == null)
            return AssemblyMotionOutcome.Fail(404, I18nKey.Error.AssemblyMotion.NotFound);

        var portal = _portals.Get(motion.PortalId)
            ?? throw new InvalidOperationException($"Portal {motion.PortalId} not found for motion {motion.Id}.");
        var type = _portals.GetType(motion.TypeId)
            ?? throw new InvalidOperationException($"Type {motion.TypeId} not found for motion {motion.Id}.");

        var problem = ReleaseProblem(portal, type, motion, author, nowUtc);
        if (problem != null)
            return problem;

        // Taken before any write: Submit below locks the type next, and the type lock never waits for the portal lock elsewhere.
        _motions.LockPortal(motion.PortalId);
        motion.ReferenceNumber = _motions.NextReferenceNumber(motion.PortalId);

        _auditLog.Log("AssemblyMotion", motion.Id, AssemblyMotionRepository.ReleasedAuditAction, AssemblyMotionRepository.ReleasedRevisionField, null, motion.Revision.ToString());

        var declaredCompetitions = _db.AssemblyMotionCompetitions
            .Where(c => c.DeclaringMotionId == motion.Id)
            .Select(c => c.MotionAId == motion.Id ? c.MotionBId : c.MotionAId)
            .ToList();

        if (type.MinSubmitters <= 1) {
            Submit(motion, nowUtc, notifications);
            notifications.Add(() => _notifier.Submitted(motion));
        } else {
            motion.Status = AssemblyMotionStatus.CollectingCoSubmitters;
            _motions.Update(motion);
            _motions.MarkForumPending(motion.Id, nowUtc);
        }
        _auditLog.Log("AssemblyMotion", motion.Id, AssemblyMotionRepository.ReleasedAuditAction, nameof(AssemblyMotion.ReferenceNumber), null,
            motion.ReferenceNumber.ToString());

        notifications.Add(() => NotifyDeclaredCompetitions(motion, declaredCompetitions));

        tx.Commit();
        RunAll(notifications);
        return AssemblyMotionOutcome.Ok(motion);
    }

    private void NotifyDeclaredCompetitions(AssemblyMotion motion, List<Guid> otherMotionIds) {
        foreach (var otherMotionId in otherMotionIds) {
            var other = _motions.Get(otherMotionId);
            if (other == null || (other.Status != AssemblyMotionStatus.Submitted && other.Status != AssemblyMotionStatus.Admitted))
                continue;

            var submitterIds = _motions.GetSubmitters(other.Id)
                .Select(s => s.MemberId)
                .ToList();
            _notifier.CompetitionDeclared(motion, other, submitterIds);
        }
    }

    public bool CanJoin(AssemblyMotionPortal portal, AssemblyMotionType type, AssemblyMotion motion, bool isAlreadySubmitter, DateTime nowUtc) {
        return JoinProblem(portal, type, motion, isAlreadySubmitter, nowUtc) == null;
    }

    private AssemblyMotionOutcome? JoinProblem(AssemblyMotionPortal portal, AssemblyMotionType type, AssemblyMotion motion, bool isAlreadySubmitter, DateTime nowUtc) {
        if (motion.Status != AssemblyMotionStatus.CollectingCoSubmitters)
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.InvalidState);

        var effectiveDeadline = AssemblyMotionDeadlines.Effective(portal, type, motion.IsCompetingMotion);
        if (AssemblyMotionDeadlines.IsPast(effectiveDeadline, nowUtc)) {
            return AssemblyMotionOutcome.Fail(409, I18nParams.With(I18nKey.Error.AssemblyMotion.DeadlinePassed,
                ("deadline", BerlinTime.FormatDeadline(effectiveDeadline))));
        }

        if (isAlreadySubmitter)
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.AlreadySubmitter);

        return null;
    }

    public AssemblyMotionOutcome Join(Guid motionId, Member member, DateTime nowUtc) {
        var notifications = new List<Action>();
        using var tx = _motions.BeginTransaction();

        var motion = _motions.GetForUpdate(motionId);
        if (motion == null)
            return AssemblyMotionOutcome.Fail(404, I18nKey.Error.AssemblyMotion.NotFound);

        var portal = _portals.Get(motion.PortalId)
            ?? throw new InvalidOperationException($"Portal {motion.PortalId} not found for motion {motion.Id}.");
        var type = _portals.GetType(motion.TypeId)
            ?? throw new InvalidOperationException($"Type {motion.TypeId} not found for motion {motion.Id}.");

        var submitters = _motions.GetSubmitters(motion.Id);
        var isAlreadySubmitter = submitters.Any(s => s.MemberId == member.Id);

        var problem = JoinProblem(portal, type, motion, isAlreadySubmitter, nowUtc);
        if (problem != null)
            return problem;

        _motions.AddSubmitter(new AssemblyMotionSubmitter {
            MotionId = motion.Id,
            MemberId = member.Id,
            IsAuthor = false,
            JoinedAt = nowUtc,
            JoinedRevision = motion.Revision
        });

        _auditLog.Log("AssemblyMotion", motion.Id, "CoSubmitterJoined");
        _motions.MarkForumPending(motion.Id, nowUtc);

        if (submitters.Count + 1 >= type.MinSubmitters) {
            Submit(motion, nowUtc, notifications);
            notifications.Add(() => _notifier.Submitted(motion));
        }

        tx.Commit();
        RunAll(notifications);
        return AssemblyMotionOutcome.Ok(motion);
    }

    public bool CanLeave(AssemblyMotion motion, Member member, bool isSubmitter) {
        return LeaveProblem(motion, member, isSubmitter) == null;
    }

    private AssemblyMotionOutcome? LeaveProblem(AssemblyMotion motion, Member member, bool isSubmitter) {
        if (motion.Status != AssemblyMotionStatus.CollectingCoSubmitters)
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.InvalidState);
        if (motion.CreatedByMemberId == member.Id)
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.AuthorCannotLeave);
        if (!isSubmitter)
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.NotSubmitter);

        return null;
    }

    public AssemblyMotionOutcome Leave(Guid motionId, Member member, DateTime nowUtc) {
        using var tx = _motions.BeginTransaction();

        var motion = _motions.GetForUpdate(motionId);
        if (motion == null)
            return AssemblyMotionOutcome.Fail(404, I18nKey.Error.AssemblyMotion.NotFound);

        var isSubmitter = _motions.GetSubmitters(motion.Id).Any(s => s.MemberId == member.Id);
        var problem = LeaveProblem(motion, member, isSubmitter);
        if (problem != null)
            return problem;

        _motions.RemoveSubmitter(motion.Id, member.Id);
        _auditLog.Log("AssemblyMotion", motion.Id, "CoSubmitterLeft");
        _motions.MarkForumPending(motion.Id, nowUtc);

        tx.Commit();
        return AssemblyMotionOutcome.Ok(motion);
    }

    public bool CanWithdraw(AssemblyMotionPortal portal, AssemblyMotion motion, Member author, DateTime nowUtc) {
        return WithdrawProblem(portal, motion, author, nowUtc) == null;
    }

    private AssemblyMotionOutcome? WithdrawProblem(AssemblyMotionPortal portal, AssemblyMotion motion, Member author, DateTime nowUtc) {
        if (motion.CreatedByMemberId != author.Id)
            return AssemblyMotionOutcome.Fail(403, I18nKey.Error.AssemblyMotion.NotAuthor);
        if (!IsWithdrawableStatus(motion.Status))
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.InvalidState);
        if (nowUtc > portal.ClosesAt)
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.PortalClosed);

        return null;
    }

    private static bool IsWithdrawableStatus(AssemblyMotionStatus status) {
        if (status == AssemblyMotionStatus.Draft)
            return false;
        if (status == AssemblyMotionStatus.Withdrawn)
            return false;

        return status != AssemblyMotionStatus.Lapsed;
    }

    public AssemblyMotionOutcome Withdraw(Guid motionId, Member author, DateTime nowUtc) {
        var notifications = new List<Action>();
        using var tx = _motions.BeginTransaction();

        var motion = _motions.GetForUpdate(motionId);
        if (motion == null)
            return AssemblyMotionOutcome.Fail(404, I18nKey.Error.AssemblyMotion.NotFound);

        var portal = _portals.Get(motion.PortalId)
            ?? throw new InvalidOperationException($"Portal {motion.PortalId} not found for motion {motion.Id}.");

        var problem = WithdrawProblem(portal, motion, author, nowUtc);
        if (problem != null)
            return problem;

        var includeCommission = motion.WasSubmitted;
        var coSubmitterIds = _motions.GetSubmitters(motion.Id)
            .Where(s => !s.IsAuthor)
            .Select(s => s.MemberId)
            .ToList();

        motion.Status = AssemblyMotionStatus.Withdrawn;
        motion.WithdrawnAt = nowUtc;
        _motions.Update(motion);
        _motions.MarkMirrorsPending(motion.Id, nowUtc);

        _auditLog.Log("AssemblyMotion", motion.Id, "Withdrawn");

        notifications.Add(() => _notifier.Withdrawn(motion, coSubmitterIds, includeCommission));

        tx.Commit();
        RunAll(notifications);
        return AssemblyMotionOutcome.Ok(motion);
    }

    public bool CanDelete(AssemblyMotion motion, Member author) {
        return DeleteProblem(motion, author) == null;
    }

    private AssemblyMotionOutcome? DeleteProblem(AssemblyMotion motion, Member author) {
        if (motion.CreatedByMemberId != author.Id)
            return AssemblyMotionOutcome.Fail(403, I18nKey.Error.AssemblyMotion.NotAuthor);
        if (motion.Status != AssemblyMotionStatus.Draft)
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.InvalidState);

        return null;
    }

    public AssemblyMotionOutcome DeleteDraft(Guid motionId, Member author, DateTime nowUtc) {
        using var tx = _motions.BeginTransaction();

        var motion = _motions.GetForUpdate(motionId);
        if (motion == null)
            return AssemblyMotionOutcome.Fail(404, I18nKey.Error.AssemblyMotion.NotFound);

        var problem = DeleteProblem(motion, author);
        if (problem != null)
            return problem;

        _motions.SoftDelete(motion.Id, nowUtc);

        tx.Commit();
        return AssemblyMotionOutcome.Ok(motion);
    }

    public int LapseOverdue(DateTime nowUtc) {
        var candidates = _motions.ListOverdueCollecting(nowUtc);
        var lapsedCount = 0;

        foreach (var candidate in candidates) {
            using var tx = _motions.BeginTransaction();

            var motion = _motions.GetForUpdate(candidate.Id);
            if (motion == null || motion.Status != AssemblyMotionStatus.CollectingCoSubmitters)
                continue;

            var portal = _portals.Get(motion.PortalId)
                ?? throw new InvalidOperationException($"Portal {motion.PortalId} not found for motion {motion.Id}.");
            var type = _portals.GetType(motion.TypeId)
                ?? throw new InvalidOperationException($"Type {motion.TypeId} not found for motion {motion.Id}.");

            var effectiveDeadline = AssemblyMotionDeadlines.Effective(portal, type, motion.IsCompetingMotion);
            if (!AssemblyMotionDeadlines.IsPast(effectiveDeadline, nowUtc))
                continue;

            var submitterIds = _motions.GetSubmitters(motion.Id)
                .Select(s => s.MemberId)
                .ToList();

            motion.Status = AssemblyMotionStatus.Lapsed;
            _motions.Update(motion);
            _motions.MarkMirrorsPending(motion.Id, nowUtc);

            _auditLog.Log("AssemblyMotion", motion.Id, "Lapsed");

            tx.Commit();
            _notifier.Lapsed(motion, submitterIds);
            lapsedCount++;
        }

        return lapsedCount;
    }

    public int SendCollectingReminders(DateTime nowUtc) {
        var candidates = _motions.ListOverdueCollecting(nowUtc)
            .Where(m => m.CollectingReminderSentAt == null)
            .ToList();
        var sentCount = 0;

        foreach (var candidate in candidates) {
            using var tx = _motions.BeginTransaction();

            var motion = _motions.GetForUpdate(candidate.Id);
            if (motion == null || motion.Status != AssemblyMotionStatus.CollectingCoSubmitters || motion.CollectingReminderSentAt != null)
                continue;

            var portal = _portals.Get(motion.PortalId)
                ?? throw new InvalidOperationException($"Portal {motion.PortalId} not found for motion {motion.Id}.");
            var type = _portals.GetType(motion.TypeId)
                ?? throw new InvalidOperationException($"Type {motion.TypeId} not found for motion {motion.Id}.");

            var effectiveDeadline = AssemblyMotionDeadlines.Effective(portal, type, motion.IsCompetingMotion);
            if (effectiveDeadline <= nowUtc || effectiveDeadline > nowUtc.Add(CollectingReminderLead))
                continue;

            motion.CollectingReminderSentAt = nowUtc;
            _motions.Update(motion);

            tx.Commit();
            _notifier.CollectingReminder(motion, effectiveDeadline);
            sentCount++;
        }

        return sentCount;
    }

    private void QueueLinkedMotionsForWiki(Guid motionId, DateTime nowUtc) {
        var linkedIds = _db.AssemblyMotionCompetitions
            .Where(c => c.MotionAId == motionId || c.MotionBId == motionId)
            .Select(c => c.MotionAId == motionId ? c.MotionBId : c.MotionAId)
            .ToList();

        var numberedIds = _db.AssemblyMotions
            .Where(m => linkedIds.Contains(m.Id) && m.Number != null && m.DeletedAt == null)
            .Select(m => m.Id)
            .ToList();

        foreach (var numberedId in numberedIds)
            _motions.MarkMirrorsPending(numberedId, nowUtc);
    }

    private static void RunAll(List<Action> notifications) {
        foreach (var notification in notifications)
            notification();
    }

    private void Submit(AssemblyMotion motion, DateTime nowUtc, List<Action> afterCommit) {
        if (motion.Number == null) {
            _motions.LockType(motion.TypeId);
            motion.Number = _motions.NextNumber(motion.PortalId, motion.TypeId);
            // The linked motions' pages list this number; they are queued after the commit so no second motion lock is taken inside this transaction.
            afterCommit.Add(() => QueueLinkedMotionsForWiki(motion.Id, nowUtc));
        }
        motion.Status = AssemblyMotionStatus.Submitted;
        motion.SubmittedAt = nowUtc;
        _motions.Update(motion);
        _motions.MarkMirrorsPending(motion.Id, nowUtc);
        _auditLog.Log("AssemblyMotion", motion.Id, "Submitted", "Number", null, motion.Number.ToString());
    }
}
