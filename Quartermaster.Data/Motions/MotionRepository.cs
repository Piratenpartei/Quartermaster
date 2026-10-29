using LinqToDB;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Motions;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.ChapterAssociates;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.MembershipApplications;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.Motions;

public class MotionRepository {
    private readonly DbContext _context;
    private readonly AuditLogRepository _auditLog;

    public MotionRepository(DbContext context, AuditLogRepository auditLog) {
        _context = context;
        _auditLog = auditLog;
    }

    public Motion? Get(Guid id)
        => _context.Motions.Where(m => m.Id == id && m.DeletedAt == null).FirstOrDefault();

    public Dictionary<Guid, Guid> GetChapterIds(IReadOnlyCollection<Guid> ids)
        => _context.Motions
            .Where(m => ids.Contains(m.Id))
            .ToDictionary(m => m.Id, m => m.ChapterId);

    /// <summary>Passes over motions closed by a handover; their replacement carries the application on.</summary>
    public Motion? GetByLinkedApplicationId(Guid applicationId)
        => _context.Motions.Where(m => m.LinkedMembershipApplicationId == applicationId && m.HandedOverToMotionId == null && m.DeletedAt == null)
            .OrderBy(m => m.CreatedAt)
            .FirstOrDefault();

    public List<Motion> ListByLinkedApplicationId(Guid applicationId)
        => _context.Motions.Where(m => m.LinkedMembershipApplicationId == applicationId && m.DeletedAt == null)
            .OrderBy(m => m.CreatedAt)
            .ToList();

    /// <summary>Passes over motions closed by a handover; their replacement carries the due selection on.</summary>
    public Motion? GetByLinkedDueSelectionId(Guid dueSelectionId)
        => _context.Motions.Where(m => m.LinkedDueSelectionId == dueSelectionId && m.HandedOverToMotionId == null && m.DeletedAt == null).FirstOrDefault();

    public void Create(Motion motion) {
        _context.Insert(motion);
        _auditLog.LogCreated("Motion", motion.Id);
    }

    public List<Motion> ListPendingInChapter(Guid chapterId)
        => _context.Motions
            .Where(m => m.ChapterId == chapterId && m.ApprovalStatus == MotionApprovalStatus.Pending && m.DeletedAt == null)
            .OrderBy(m => m.CreatedAt)
            .ToList();

    public void SetHandedOverTo(Guid id, Guid newMotionId) {
        _context.Motions
            .Where(m => m.Id == id)
            .Set(m => m.HandedOverToMotionId, (Guid?)newMotionId)
            .Update();
        _auditLog.Log("Motion", id, "HandedOver", nameof(Motion.HandedOverToMotionId), null, newMotionId.ToString());
    }

    public (List<Motion> Items, int TotalCount) List(
        Guid? chapterId, MotionApprovalStatus? status, bool includeNonPublic, int page, int pageSize,
        List<Guid>? nonPublicChapterIds = null) {

        var q = _context.Motions.Where(m => m.DeletedAt == null).AsQueryable();

        if (chapterId.HasValue)
            q = q.Where(m => m.ChapterId == chapterId.Value);

        if (status != null)
            q = q.Where(m => m.ApprovalStatus == status.Value);

        if (!includeNonPublic) {
            q = q.Where(m => m.IsPublic);
        } else if (nonPublicChapterIds != null) {
            // User has ViewMotions on specific chapters only — show public motions
            // everywhere, plus non-public motions from permitted chapters.
            q = q.Where(m => m.IsPublic || nonPublicChapterIds.Contains(m.ChapterId));
        }
        // nonPublicChapterIds == null means global permission — no filtering needed.

        var totalCount = q.Count();
        var items = q.OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (items, totalCount);
    }

    public int CountOpen(List<Guid>? allowedChapterIds) {
        var q = _context.Motions
            .Where(m => m.DeletedAt == null && m.ApprovalStatus == MotionApprovalStatus.Pending);

        if (allowedChapterIds != null)
            q = q.Where(m => allowedChapterIds.Contains(m.ChapterId));

        return q.Count();
    }

    public (List<Motion> Items, int TotalCount) ListOpen(List<Guid>? allowedChapterIds, int limit) {
        var q = _context.Motions
            .Where(m => m.DeletedAt == null && m.ApprovalStatus == MotionApprovalStatus.Pending);

        if (allowedChapterIds != null)
            q = q.Where(m => allowedChapterIds.Contains(m.ChapterId));

        var total = q.Count();
        var items = q.OrderByDescending(m => m.CreatedAt).Take(limit).ToList();
        return (items, total);
    }

    public List<MotionVote> GetVotes(Guid motionId)
        => _context.MotionVotes.Where(v => v.MotionId == motionId).ToList();

    public MotionVote? GetVote(Guid motionId, Guid memberId)
        => _context.MotionVotes
            .Where(v => v.MotionId == motionId && v.MemberId == memberId)
            .FirstOrDefault();

    public void CastVote(MotionVote vote) {
        var existing = GetVote(vote.MotionId, vote.MemberId);
        if (existing != null) {
            _context.MotionVotes
                .Where(v => v.Id == existing.Id)
                .Set(v => v.Vote, vote.Vote)
                .Set(v => v.VotedAt, vote.VotedAt)
                .Set(v => v.MeetingId, vote.MeetingId)
                .Set(v => v.CastByUserId, vote.CastByUserId)
                .Update();

            _auditLog.LogFieldChange("MotionVote", existing.Id, "Vote", existing.Vote.ToString(), vote.Vote.ToString());
        } else {
            _context.Insert(vote);
            _auditLog.LogCreated("MotionVote", vote.Id);
        }
    }

    public bool TryAutoResolve(Guid motionId, ChapterOfficerRepository officerRepo) {
        var motion = Get(motionId);
        if (motion == null || motion.ApprovalStatus != MotionApprovalStatus.Pending)
            return false;

        var officerCount = officerRepo.CountForChapter(motion.ChapterId);
        if (officerCount == 0)
            return false;

        var votes = GetVotes(motionId);

        // If any vote was cast in the context of a meeting, skip auto-resolve.
        // Meeting-linked motions require explicit close (via close-vote endpoint or the
        // on-complete sweep). Manual resolve via MotionStatusEndpoint still works.
        if (votes.Any(v => v.MeetingId != null))
            return false;

        var approveCount = votes.Count(v => v.Vote == VoteType.Approve);
        var denyCount = votes.Count(v => v.Vote == VoteType.Deny);
        var majority = (officerCount / 2) + 1;

        MotionApprovalStatus? newStatus = null;
        if (approveCount >= majority)
            newStatus = MotionApprovalStatus.Approved;
        else if (denyCount >= majority)
            newStatus = MotionApprovalStatus.Rejected;

        if (newStatus == null)
            return false;

        using var tx = _context.BeginTransaction();

        _context.Motions
            .Where(m => m.Id == motionId)
            .Set(m => m.ApprovalStatus, newStatus.Value)
            .Set(m => m.ResolvedAt, DateTime.UtcNow)
            .Update();

        _auditLog.LogFieldChange("Motion", motionId, "ApprovalStatus", motion.ApprovalStatus.ToString(), newStatus.Value.ToString());

        CascadeResolutionToLinkedEntities(motion, newStatus.Value);

        tx.Commit();
        return true;
    }

    public void UpdateApprovalStatus(Guid id, MotionApprovalStatus status) {
        var existing = _context.Motions.Where(m => m.Id == id).FirstOrDefault();
        if (existing == null)
            return;

        using var tx = _context.BeginTransaction();

        _context.Motions
            .Where(m => m.Id == id)
            .Set(m => m.ApprovalStatus, status)
            .Set(m => m.ResolvedAt, DateTime.UtcNow)
            .Update();

        _auditLog.LogFieldChange("Motion", id, "ApprovalStatus", existing.ApprovalStatus.ToString(), status.ToString());

        CascadeResolutionToLinkedEntities(existing, status);

        tx.Commit();
    }

    /// <summary>
    /// <see cref="UpdateApprovalStatus"/> only while the motion is still Pending; false when a vote or another closing got there first.
    /// </summary>
    public bool UpdatePendingApprovalStatus(Guid id, MotionApprovalStatus status) {
        // Read before the transaction: under snapshot isolation a read inside it would turn a lost race into an error instead of false.
        var existing = _context.Motions.Where(m => m.Id == id).FirstOrDefault();
        if (existing == null)
            return false;

        using var tx = _context.BeginTransaction();
        if (!UpdatePendingApprovalStatus(existing, status))
            return false;

        tx.Commit();
        return true;
    }

    /// <summary><see cref="UpdatePendingApprovalStatus(Guid, MotionApprovalStatus)"/> inside the caller's transaction.</summary>
    public bool UpdatePendingApprovalStatusInTransaction(Guid id, MotionApprovalStatus status) {
        var existing = _context.Motions.Where(m => m.Id == id).FirstOrDefault();
        if (existing == null)
            return false;
        return UpdatePendingApprovalStatus(existing, status);
    }

    private bool UpdatePendingApprovalStatus(Motion existing, MotionApprovalStatus status) {
        var id = existing.Id;
        var updated = _context.Motions
            .Where(m => m.Id == id && m.ApprovalStatus == MotionApprovalStatus.Pending)
            .Set(m => m.ApprovalStatus, status)
            .Set(m => m.ResolvedAt, DateTime.UtcNow)
            .Update();
        if (updated == 0)
            return false;

        _auditLog.LogFieldChange("Motion", id, "ApprovalStatus", MotionApprovalStatus.Pending.ToString(), status.ToString());

        CascadeResolutionToLinkedEntities(existing, status);
        return true;
    }

    /// <summary>
    /// Rewrites the text without counting as a substantive edit: unlike <see cref="Update"/>, the votes cast so far stay. Runs inside the
    /// caller's transaction.
    /// </summary>
    public void UpdateText(Guid id, string textMarkdown, string textHtml) {
        var existing = _context.Motions.Where(m => m.Id == id && m.DeletedAt == null).FirstOrDefault();
        if (existing == null || existing.TextMarkdown == textMarkdown)
            return;

        _context.Motions
            .Where(m => m.Id == id)
            .Set(m => m.TextMarkdown, textMarkdown)
            .Set(m => m.Text, textHtml)
            .Update();
        _auditLog.LogFieldChange("Motion", id, nameof(Motion.TextMarkdown), existing.TextMarkdown, textMarkdown);
    }

    /// <summary>
    /// Propagates a motion resolution to its linked membership application and/or due selection.
    /// <c>ClosedWithoutAction</c> (and any non-terminal status) leaves the linked entity untouched —
    /// only a real approve/reject decision flips it. Must run inside the caller's transaction.
    /// </summary>
    private void CascadeResolutionToLinkedEntities(Motion motion, MotionApprovalStatus newStatus) {
        var appStatus = MapToApplicationStatus(newStatus);
        if (motion.LinkedMembershipApplicationId.HasValue && appStatus.HasValue)
            CascadeToApplication(motion.LinkedMembershipApplicationId.Value, motion.Id, appStatus.Value);

        var dsStatus = MapToDueSelectionStatus(newStatus);
        if (motion.LinkedDueSelectionId.HasValue && dsStatus.HasValue) {
            _context.DueSelections
                .Where(d => d.Id == motion.LinkedDueSelectionId.Value)
                .Set(d => d.Status, dsStatus.Value)
                .Set(d => d.ProcessedAt, DateTime.UtcNow)
                .Update();
        }
    }

    private void CascadeToApplication(Guid applicationId, Guid motionId, ApplicationStatus resolvedStatus) {
        var application = _context.MembershipApplications.Where(a => a.Id == applicationId).FirstOrDefault();
        if (application == null)
            return;
        if (!MayDecide(application, motionId))
            return;

        var status = resolvedStatus == ApplicationStatus.Approved ? application.ApprovedStatus(GetRootChapterId()) : resolvedStatus;

        var readStatus = application.Status;
        var update = _context.MembershipApplications
            .Where(a => a.Id == applicationId && a.Status == readStatus)
            .Set(a => a.Status, status);
        if (status != ApplicationStatus.PendingFederalApproval)
            update = update.Set(a => a.ProcessedAt, (DateTime?)DateTime.UtcNow);
        update.Update();
    }

    /// <summary>
    /// A motion still open after its application was decided in the processing dialog may be voted on later; that vote must not override the
    /// decision. Once the chapter approved, only the federal motion decides.
    /// </summary>
    private static bool MayDecide(MembershipApplication application, Guid motionId) {
        if (application.Status == ApplicationStatus.Pending || application.Status == ApplicationStatus.PendingDivisionLinking)
            return true;
        return application.Status == ApplicationStatus.PendingFederalApproval && application.FederalApprovalMotionId == motionId;
    }

    private Guid? GetRootChapterId()
        => _context.Chapters
            .Where(c => c.ParentChapterId == null && c.DeletedAt == null)
            .OrderBy(c => c.Name)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefault();

    private static ApplicationStatus? MapToApplicationStatus(MotionApprovalStatus status) => status switch {
        MotionApprovalStatus.Approved => ApplicationStatus.Approved,
        MotionApprovalStatus.Rejected => ApplicationStatus.Rejected,
        MotionApprovalStatus.FormallyRejected => ApplicationStatus.Rejected,
        _ => null
    };

    private static DueSelectionStatus? MapToDueSelectionStatus(MotionApprovalStatus status) => status switch {
        MotionApprovalStatus.Approved => DueSelectionStatus.Approved,
        MotionApprovalStatus.Rejected => DueSelectionStatus.Rejected,
        MotionApprovalStatus.FormallyRejected => DueSelectionStatus.Rejected,
        _ => null
    };

    /// <summary>
    /// Field-level substantive edit. Performs a per-field diff against the stored row, persists
    /// only the changed columns, and emits one <see cref="AuditLogRepository.LogFieldChange"/>
    /// per change — all inside a single transaction. The caller (endpoint) is responsible for
    /// the permission and lifecycle gates (e.g. only allow while ApprovalStatus == Pending).
    /// </summary>
    public void Update(
        Guid id,
        string title,
        string textMarkdown,
        string textHtml,
        string authorName,
        string authorEmail,
        Guid? linkedMembershipApplicationId,
        Guid? linkedDueSelectionId) {

        var existing = _context.Motions.Where(m => m.Id == id && m.DeletedAt == null).FirstOrDefault();
        if (existing == null)
            return;

        using var tx = _context.BeginTransaction();

        if (existing.Title != title) {
            _context.Motions.Where(m => m.Id == id).Set(m => m.Title, title).Update();
            _auditLog.LogFieldChange("Motion", id, nameof(Motion.Title), existing.Title, title);
        }

        if (existing.TextMarkdown != textMarkdown) {
            _context.Motions
                .Where(m => m.Id == id)
                .Set(m => m.TextMarkdown, textMarkdown)
                .Set(m => m.Text, textHtml)
                .Update();
            _auditLog.LogFieldChange("Motion", id, nameof(Motion.TextMarkdown), existing.TextMarkdown, textMarkdown);

            var staleVotes = _context.MotionVotes.Where(v => v.MotionId == id).ToList();
            foreach (var v in staleVotes)
                _auditLog.LogDeleted("MotionVote", v.Id);
            _context.MotionVotes.Where(v => v.MotionId == id).Delete();
        }

        if (existing.AuthorName != authorName) {
            _context.Motions.Where(m => m.Id == id).Set(m => m.AuthorName, authorName).Update();
            _auditLog.LogFieldChange("Motion", id, nameof(Motion.AuthorName), existing.AuthorName, authorName);
        }

        if (existing.AuthorEmail != authorEmail) {
            _context.Motions.Where(m => m.Id == id).Set(m => m.AuthorEmail, authorEmail).Update();
            _auditLog.LogFieldChange("Motion", id, nameof(Motion.AuthorEmail), existing.AuthorEmail, authorEmail);
        }

        if (existing.LinkedMembershipApplicationId != linkedMembershipApplicationId) {
            _context.Motions
                .Where(m => m.Id == id)
                .Set(m => m.LinkedMembershipApplicationId, linkedMembershipApplicationId)
                .Update();
            _auditLog.LogFieldChange("Motion", id, nameof(Motion.LinkedMembershipApplicationId),
                existing.LinkedMembershipApplicationId?.ToString(), linkedMembershipApplicationId?.ToString());
        }

        if (existing.LinkedDueSelectionId != linkedDueSelectionId) {
            _context.Motions
                .Where(m => m.Id == id)
                .Set(m => m.LinkedDueSelectionId, linkedDueSelectionId)
                .Update();
            _auditLog.LogFieldChange("Motion", id, nameof(Motion.LinkedDueSelectionId),
                existing.LinkedDueSelectionId?.ToString(), linkedDueSelectionId?.ToString());
        }

        tx.Commit();
    }

    public void SetRealized(Guid id, bool realized) {
        var existing = _context.Motions.Where(m => m.Id == id).FirstOrDefault();

        _context.Motions
            .Where(m => m.Id == id)
            .Set(m => m.IsRealized, realized)
            .Update();

        _auditLog.LogFieldChange("Motion", id, "IsRealized", existing?.IsRealized.ToString(), realized.ToString());
    }

    public void SetPublic(Guid id, bool isPublic) {
        var existing = _context.Motions.Where(m => m.Id == id).FirstOrDefault();

        _context.Motions
            .Where(m => m.Id == id)
            .Set(m => m.IsPublic, isPublic)
            .Update();

        _auditLog.LogFieldChange("Motion", id, "IsPublic", existing?.IsPublic.ToString(), isPublic.ToString());
    }

    public void SoftDelete(Guid id) {
        _context.Motions.Where(x => x.Id == id).Set(x => x.DeletedAt, DateTime.UtcNow).Update();
        _auditLog.LogSoftDeleted("Motion", id);
    }
}
