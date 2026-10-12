using System;
using System.Linq;
using LinqToDB;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Data;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Members;
using Quartermaster.Rendering;

namespace Quartermaster.Server.AssemblyMotions;

public class AssemblyMotionService {
    private readonly AssemblyMotionRepository _motions;
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly AuditLogRepository _auditLog;
    private readonly DbContext _db;

    public AssemblyMotionService(AssemblyMotionRepository motions, AssemblyMotionPortalRepository portals,
        AuditLogRepository auditLog, DbContext db) {
        _motions = motions;
        _portals = portals;
        _auditLog = auditLog;
        _db = db;
    }

    public AssemblyMotionOutcome CreateDraft(AssemblyMotionPortal portal, Member author, Guid userId, AssemblyMotionDraft draft, DateTime nowUtc) {
        if (nowUtc < portal.OpensAt)
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.PortalNotOpen);
        if (nowUtc > portal.ClosesAt)
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.PortalClosed);

        var type = _portals.GetType(draft.TypeId);
        if (type == null || type.PortalId != portal.Id)
            return AssemblyMotionOutcome.Fail(422, I18nKey.Error.AssemblyMotion.TypeNotInPortal);

        using var tx = _motions.BeginTransaction();

        var motion = new AssemblyMotion {
            PortalId = portal.Id,
            TypeId = draft.TypeId,
            Title = draft.Title,
            Section = draft.Section,
            TextMarkdown = draft.TextMarkdown,
            TextHtml = MarkdownService.ToHtml(draft.TextMarkdown, SanitizationProfile.Standard, MarkdownFlavor.Motion),
            ReasonMarkdown = draft.ReasonMarkdown,
            ReasonHtml = MarkdownService.ToHtml(draft.ReasonMarkdown, SanitizationProfile.Standard, MarkdownFlavor.Motion),
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

    public AssemblyMotionOutcome UpdateDraft(AssemblyMotion motion, Member author, Guid userId, AssemblyMotionDraft draft, int baseRevision, DateTime nowUtc) {
        using var tx = _motions.BeginTransaction();

        var locked = _motions.GetForUpdate(motion.Id);
        if (locked == null)
            return AssemblyMotionOutcome.Fail(404, I18nKey.Error.AssemblyMotion.NotFound);
        if (locked.CreatedByMemberId != author.Id)
            return AssemblyMotionOutcome.Fail(403, I18nKey.Error.AssemblyMotion.NotAuthor);
        if (locked.Status != AssemblyMotionStatus.Draft)
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.InvalidState);
        if (baseRevision != locked.Revision)
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.RevisionConflict);

        var type = _portals.GetType(draft.TypeId);
        if (type == null || type.PortalId != locked.PortalId)
            return AssemblyMotionOutcome.Fail(422, I18nKey.Error.AssemblyMotion.TypeNotInPortal);

        var oldTitle = locked.Title;
        var oldSection = locked.Section;
        var oldTextMarkdown = locked.TextMarkdown;
        var oldReasonMarkdown = locked.ReasonMarkdown;
        var oldSummary = locked.Summary;
        var oldKeywords = locked.Keywords;
        var oldPriorDiscussion = locked.PriorDiscussion;

        locked.TypeId = draft.TypeId;
        locked.Title = draft.Title;
        locked.Section = draft.Section;
        locked.TextMarkdown = draft.TextMarkdown;
        locked.TextHtml = MarkdownService.ToHtml(draft.TextMarkdown, SanitizationProfile.Standard, MarkdownFlavor.Motion);
        locked.ReasonMarkdown = draft.ReasonMarkdown;
        locked.ReasonHtml = MarkdownService.ToHtml(draft.ReasonMarkdown, SanitizationProfile.Standard, MarkdownFlavor.Motion);
        locked.Summary = draft.Summary;
        locked.Keywords = draft.Keywords;
        locked.PriorDiscussion = draft.PriorDiscussion;
        locked.Revision += 1;
        _motions.Update(locked);

        _db.AssemblyMotionSubmitters
            .Where(s => s.MotionId == locked.Id && s.IsAuthor)
            .Set(s => s.JoinedRevision, locked.Revision)
            .Update();

        _motions.AddRevision(new AssemblyMotionRevision {
            MotionId = locked.Id,
            Revision = locked.Revision,
            Title = locked.Title,
            Section = locked.Section,
            TextMarkdown = locked.TextMarkdown,
            ReasonMarkdown = locked.ReasonMarkdown,
            Summary = locked.Summary,
            Keywords = locked.Keywords,
            PriorDiscussion = locked.PriorDiscussion,
            EditedByUserId = userId,
            EditedAt = nowUtc,
            EditedByCommission = false
        });

        _auditLog.LogFieldChange("AssemblyMotion", locked.Id, nameof(AssemblyMotion.Title), oldTitle, locked.Title);
        _auditLog.LogFieldChange("AssemblyMotion", locked.Id, nameof(AssemblyMotion.Section), oldSection, locked.Section);
        _auditLog.LogFieldChange("AssemblyMotion", locked.Id, nameof(AssemblyMotion.TextMarkdown), oldTextMarkdown, locked.TextMarkdown);
        _auditLog.LogFieldChange("AssemblyMotion", locked.Id, nameof(AssemblyMotion.ReasonMarkdown), oldReasonMarkdown, locked.ReasonMarkdown);
        _auditLog.LogFieldChange("AssemblyMotion", locked.Id, nameof(AssemblyMotion.Summary), oldSummary, locked.Summary);
        _auditLog.LogFieldChange("AssemblyMotion", locked.Id, nameof(AssemblyMotion.Keywords), oldKeywords, locked.Keywords);
        _auditLog.LogFieldChange("AssemblyMotion", locked.Id, nameof(AssemblyMotion.PriorDiscussion), oldPriorDiscussion, locked.PriorDiscussion);

        tx.Commit();
        return AssemblyMotionOutcome.Ok(locked);
    }

    public bool CanEdit(AssemblyMotion motion, Member author)
        => motion.CreatedByMemberId == author.Id && motion.Status == AssemblyMotionStatus.Draft;

    public bool CanRelease(AssemblyMotionPortal portal, AssemblyMotionType type, AssemblyMotion motion, Member author, DateTime nowUtc)
        => ReleaseProblem(portal, type, motion, author, nowUtc) == null;

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

        _auditLog.Log("AssemblyMotion", motion.Id, "Released");

        if (type.MinSubmitters <= 1) {
            Submit(motion, nowUtc);
        } else {
            motion.Status = AssemblyMotionStatus.CollectingCoSubmitters;
            _motions.Update(motion);
        }

        tx.Commit();
        return AssemblyMotionOutcome.Ok(motion);
    }

    public bool CanJoin(AssemblyMotionPortal portal, AssemblyMotionType type, AssemblyMotion motion, bool isAlreadySubmitter, DateTime nowUtc)
        => JoinProblem(portal, type, motion, isAlreadySubmitter, nowUtc) == null;

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

        if (submitters.Count + 1 >= type.MinSubmitters)
            Submit(motion, nowUtc);

        tx.Commit();
        return AssemblyMotionOutcome.Ok(motion);
    }

    public bool CanLeave(AssemblyMotion motion, Member member, bool isSubmitter)
        => LeaveProblem(motion, member, isSubmitter) == null;

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

        tx.Commit();
        return AssemblyMotionOutcome.Ok(motion);
    }

    public bool CanWithdraw(AssemblyMotionPortal portal, AssemblyMotion motion, Member author, DateTime nowUtc)
        => WithdrawProblem(portal, motion, author, nowUtc) == null;

    private AssemblyMotionOutcome? WithdrawProblem(AssemblyMotionPortal portal, AssemblyMotion motion, Member author, DateTime nowUtc) {
        if (motion.CreatedByMemberId != author.Id)
            return AssemblyMotionOutcome.Fail(403, I18nKey.Error.AssemblyMotion.NotAuthor);
        if (motion.Status == AssemblyMotionStatus.Withdrawn || motion.Status == AssemblyMotionStatus.Lapsed)
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.InvalidState);
        if (nowUtc > portal.ClosesAt)
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.PortalClosed);

        return null;
    }

    public AssemblyMotionOutcome Withdraw(Guid motionId, Member author, DateTime nowUtc) {
        using var tx = _motions.BeginTransaction();

        var motion = _motions.GetForUpdate(motionId);
        if (motion == null)
            return AssemblyMotionOutcome.Fail(404, I18nKey.Error.AssemblyMotion.NotFound);

        var portal = _portals.Get(motion.PortalId)
            ?? throw new InvalidOperationException($"Portal {motion.PortalId} not found for motion {motion.Id}.");

        var problem = WithdrawProblem(portal, motion, author, nowUtc);
        if (problem != null)
            return problem;

        motion.Status = AssemblyMotionStatus.Withdrawn;
        motion.WithdrawnAt = nowUtc;
        _motions.Update(motion);

        if (motion.WasSubmitted)
            _motions.MarkWikiPending(motion.Id, nowUtc);

        _auditLog.Log("AssemblyMotion", motion.Id, "Withdrawn");

        tx.Commit();
        return AssemblyMotionOutcome.Ok(motion);
    }

    public bool CanDelete(AssemblyMotion motion, Member author)
        => DeleteProblem(motion, author) == null;

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

            motion.Status = AssemblyMotionStatus.Lapsed;
            _motions.Update(motion);

            if (motion.WasSubmitted)
                _motions.MarkWikiPending(motion.Id, nowUtc);

            _auditLog.Log("AssemblyMotion", motion.Id, "Lapsed");

            tx.Commit();
            lapsedCount++;
        }

        return lapsedCount;
    }

    private void Submit(AssemblyMotion motion, DateTime nowUtc) {
        if (motion.Number == null) {
            _motions.LockType(motion.TypeId);
            motion.Number = _motions.NextNumber(motion.PortalId, motion.TypeId);
        }
        motion.Status = AssemblyMotionStatus.Submitted;
        motion.SubmittedAt = nowUtc;
        _motions.Update(motion);
        _motions.MarkWikiPending(motion.Id, nowUtc);
        _auditLog.Log("AssemblyMotion", motion.Id, "Submitted", "Number", null, motion.Number.ToString());
    }
}
