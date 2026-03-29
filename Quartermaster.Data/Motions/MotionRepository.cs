using LinqToDB;
using Quartermaster.Data.ChapterAssociates;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.MembershipApplications;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.Motions;

public class MotionRepository {
    private readonly DbContext _context;

    public MotionRepository(DbContext context) {
        _context = context;
    }

    public Motion? Get(Guid id)
        => _context.Motions.Where(m => m.Id == id).FirstOrDefault();

    public Motion? GetByLinkedApplicationId(Guid applicationId)
        => _context.Motions.Where(m => m.LinkedMembershipApplicationId == applicationId).FirstOrDefault();

    public Motion? GetByLinkedDueSelectionId(Guid dueSelectionId)
        => _context.Motions.Where(m => m.LinkedDueSelectionId == dueSelectionId).FirstOrDefault();

    public void Create(Motion motion) => _context.Insert(motion);

    public (List<Motion> Items, int TotalCount) List(
        Guid? chapterId, MotionApprovalStatus? status, bool includeNonPublic, int page, int pageSize) {

        var q = _context.Motions.AsQueryable();

        if (chapterId.HasValue)
            q = q.Where(m => m.ChapterId == chapterId.Value);

        if (status != null)
            q = q.Where(m => m.ApprovalStatus == status.Value);

        if (!includeNonPublic)
            q = q.Where(m => m.IsPublic);

        var totalCount = q.Count();
        var items = q.OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (items, totalCount);
    }

    public List<MotionVote> GetVotes(Guid motionId)
        => _context.MotionVotes.Where(v => v.MotionId == motionId).ToList();

    public MotionVote? GetVote(Guid motionId, Guid userId)
        => _context.MotionVotes
            .Where(v => v.MotionId == motionId && v.UserId == userId)
            .FirstOrDefault();

    public void CastVote(MotionVote vote) {
        var existing = GetVote(vote.MotionId, vote.UserId);
        if (existing != null) {
            _context.MotionVotes
                .Where(v => v.Id == existing.Id)
                .Set(v => v.Vote, vote.Vote)
                .Set(v => v.VotedAt, vote.VotedAt)
                .Update();
        } else {
            _context.Insert(vote);
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

        _context.Motions
            .Where(m => m.Id == motionId)
            .Set(m => m.ApprovalStatus, newStatus.Value)
            .Set(m => m.ResolvedAt, DateTime.UtcNow)
            .Update();

        if (motion.LinkedMembershipApplicationId.HasValue) {
            var appStatus = newStatus == MotionApprovalStatus.Approved
                ? ApplicationStatus.Approved
                : ApplicationStatus.Rejected;
            _context.MembershipApplications
                .Where(a => a.Id == motion.LinkedMembershipApplicationId.Value)
                .Set(a => a.Status, appStatus)
                .Set(a => a.ProcessedAt, DateTime.UtcNow)
                .Update();
        }

        if (motion.LinkedDueSelectionId.HasValue) {
            var dsStatus = newStatus == MotionApprovalStatus.Approved
                ? DueSelectionStatus.Approved
                : DueSelectionStatus.Rejected;
            _context.DueSelections
                .Where(d => d.Id == motion.LinkedDueSelectionId.Value)
                .Set(d => d.Status, dsStatus)
                .Set(d => d.ProcessedAt, DateTime.UtcNow)
                .Update();
        }

        return true;
    }

    public void UpdateApprovalStatus(Guid id, MotionApprovalStatus status) {
        _context.Motions
            .Where(m => m.Id == id)
            .Set(m => m.ApprovalStatus, status)
            .Set(m => m.ResolvedAt, DateTime.UtcNow)
            .Update();
    }

    public void SetRealized(Guid id, bool realized) {
        _context.Motions
            .Where(m => m.Id == id)
            .Set(m => m.IsRealized, realized)
            .Update();
    }
}
