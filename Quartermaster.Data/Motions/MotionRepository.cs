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

    public Motion? GetByLinkedApplicationId(Guid applicationId)
        => _context.Motions.Where(m => m.LinkedMembershipApplicationId == applicationId && m.DeletedAt == null).FirstOrDefault();

    public Motion? GetByLinkedDueSelectionId(Guid dueSelectionId)
        => _context.Motions.Where(m => m.LinkedDueSelectionId == dueSelectionId && m.DeletedAt == null).FirstOrDefault();

    public void Create(Motion motion) {
        _context.Insert(motion);
        _auditLog.LogCreated("Motion", motion.Id);
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
    /// Propagates a motion resolution to its linked membership application and/or due selection.
    /// <c>ClosedWithoutAction</c> (and any non-terminal status) leaves the linked entity untouched —
    /// only a real approve/reject decision flips it. Must run inside the caller's transaction.
    /// </summary>
    private void CascadeResolutionToLinkedEntities(Motion motion, MotionApprovalStatus newStatus) {
        var appStatus = MapToApplicationStatus(newStatus);
        if (motion.LinkedMembershipApplicationId.HasValue && appStatus.HasValue) {
            _context.MembershipApplications
                .Where(a => a.Id == motion.LinkedMembershipApplicationId.Value)
                .Set(a => a.Status, appStatus.Value)
                .Set(a => a.ProcessedAt, DateTime.UtcNow)
                .Update();
        }

        var dsStatus = MapToDueSelectionStatus(newStatus);
        if (motion.LinkedDueSelectionId.HasValue && dsStatus.HasValue) {
            _context.DueSelections
                .Where(d => d.Id == motion.LinkedDueSelectionId.Value)
                .Set(d => d.Status, dsStatus.Value)
                .Set(d => d.ProcessedAt, DateTime.UtcNow)
                .Update();
        }
    }

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
