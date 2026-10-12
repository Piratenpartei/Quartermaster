using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using LinqToDB;
using LinqToDB.Data;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data.AuditLog;

namespace Quartermaster.Data.AssemblyMotions;

public class AssemblyMotionRepository {
    private readonly DbContext _context;
    private readonly AuditLogRepository _auditLog;

    public AssemblyMotionRepository(DbContext context, AuditLogRepository auditLog) {
        _context = context;
        _auditLog = auditLog;
    }

    // READ COMMITTED so reads after a lock wait see rows committed meanwhile (e.g. the numbers handed out under LockType); REPEATABLE READ would keep the earlier snapshot.
    public DataConnectionTransaction BeginTransaction() => _context.BeginTransaction(IsolationLevel.ReadCommitted);

    public AssemblyMotion? Get(Guid id)
        => _context.AssemblyMotions.Where(m => m.Id == id && m.DeletedAt == null).FirstOrDefault();

    public AssemblyMotion? GetForUpdate(Guid id) {
        return _context.Query<AssemblyMotion>(
            $"SELECT * FROM {AssemblyMotion.TableName} WHERE Id = @id AND DeletedAt IS NULL FOR UPDATE",
            new DataParameter("id", id)).FirstOrDefault();
    }

    public void LockType(Guid typeId)
        => _context.Query<Guid>(
            $"SELECT Id FROM {AssemblyMotionType.TableName} WHERE Id = @id FOR UPDATE",
            new DataParameter("id", typeId)).FirstOrDefault();

    public int NextNumber(Guid portalId, Guid typeId) {
        var maxNumber = _context.AssemblyMotions
            .Where(m => m.PortalId == portalId && m.TypeId == typeId && m.Number != null)
            .Select(m => m.Number)
            .Max();

        return (maxNumber ?? 0) + 1;
    }

    public void Insert(AssemblyMotion motion) => _context.Insert(motion);

    public void Update(AssemblyMotion motion) {
        _context.AssemblyMotions
            .Where(m => m.Id == motion.Id)
            .Set(m => m.TypeId, motion.TypeId)
            .Set(m => m.Number, motion.Number)
            .Set(m => m.Title, motion.Title)
            .Set(m => m.Section, motion.Section)
            .Set(m => m.TextMarkdown, motion.TextMarkdown)
            .Set(m => m.TextHtml, motion.TextHtml)
            .Set(m => m.ReasonMarkdown, motion.ReasonMarkdown)
            .Set(m => m.ReasonHtml, motion.ReasonHtml)
            .Set(m => m.Summary, motion.Summary)
            .Set(m => m.Keywords, motion.Keywords)
            .Set(m => m.PriorDiscussion, motion.PriorDiscussion)
            .Set(m => m.IsCompetingMotion, motion.IsCompetingMotion)
            .Set(m => m.Status, motion.Status)
            .Set(m => m.Result, motion.Result)
            .Set(m => m.Revision, motion.Revision)
            .Set(m => m.CheckNote, motion.CheckNote)
            .Set(m => m.CheckedAt, motion.CheckedAt)
            .Set(m => m.CheckedByUserId, motion.CheckedByUserId)
            .Set(m => m.SubmittedAt, motion.SubmittedAt)
            .Set(m => m.WithdrawnAt, motion.WithdrawnAt)
            .Set(m => m.CollectingReminderSentAt, motion.CollectingReminderSentAt)
            .Set(m => m.WikiSyncState, motion.WikiSyncState)
            .Set(m => m.WikiSyncedRevision, motion.WikiSyncedRevision)
            .Set(m => m.WikiSyncedHash, motion.WikiSyncedHash)
            .Set(m => m.WikiLastSyncedAt, motion.WikiLastSyncedAt)
            .Set(m => m.WikiLastError, motion.WikiLastError)
            .Set(m => m.WikiFailureCount, motion.WikiFailureCount)
            .Set(m => m.WikiNextAttemptAt, motion.WikiNextAttemptAt)
            .Set(m => m.WikiSyncRequestedAt, motion.WikiSyncRequestedAt)
            .Set(m => m.OpenSlidesMotionId, motion.OpenSlidesMotionId)
            .Set(m => m.OpenSlidesPushedRevision, motion.OpenSlidesPushedRevision)
            .Set(m => m.OpenSlidesPushedAt, motion.OpenSlidesPushedAt)
            .Set(m => m.OpenSlidesLastError, motion.OpenSlidesLastError)
            .Set(m => m.DeletedAt, motion.DeletedAt)
            .Update();
    }

    public List<AssemblyMotionSubmitter> GetSubmitters(Guid motionId)
        => _context.AssemblyMotionSubmitters
            .Where(s => s.MotionId == motionId)
            .OrderByDescending(s => s.IsAuthor)
            .ThenBy(s => s.JoinedAt)
            .ToList();

    public Dictionary<Guid, List<AssemblyMotionSubmitter>> GetSubmitters(IReadOnlyCollection<Guid> motionIds) {
        return _context.AssemblyMotionSubmitters
            .Where(s => motionIds.Contains(s.MotionId))
            .OrderByDescending(s => s.IsAuthor)
            .ThenBy(s => s.JoinedAt)
            .ToList()
            .GroupBy(s => s.MotionId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    public void AddSubmitter(AssemblyMotionSubmitter submitter) => _context.Insert(submitter);

    public void RemoveSubmitter(Guid motionId, Guid memberId)
        => _context.AssemblyMotionSubmitters
            .Where(s => s.MotionId == motionId && s.MemberId == memberId)
            .Delete();

    public List<Guid> RemoveCoSubmitters(Guid motionId) {
        var memberIds = _context.AssemblyMotionSubmitters
            .Where(s => s.MotionId == motionId && !s.IsAuthor)
            .Select(s => s.MemberId)
            .ToList();

        _context.AssemblyMotionSubmitters
            .Where(s => s.MotionId == motionId && !s.IsAuthor)
            .Delete();

        return memberIds;
    }

    public void AddRevision(AssemblyMotionRevision revision) => _context.Insert(revision);

    public List<AssemblyMotionRevision> GetRevisions(Guid motionId)
        => _context.AssemblyMotionRevisions
            .Where(r => r.MotionId == motionId)
            .OrderBy(r => r.Revision)
            .ToList();

    public AssemblyMotionRevision? GetRevision(Guid motionId, int revision)
        => _context.AssemblyMotionRevisions
            .Where(r => r.MotionId == motionId && r.Revision == revision)
            .FirstOrDefault();

    public List<AssemblyMotion> ListByPortal(Guid portalId)
        => _context.AssemblyMotions
            .Where(m => m.PortalId == portalId && m.DeletedAt == null)
            .ToList();

    /// <summary>Candidate motions for the deadline job.</summary>
    public List<AssemblyMotion> ListOverdueCollecting(DateTime nowUtc) {
        // nowUtc is accepted for the caller's benefit only; the effective deadline depends on
        // the type's Deadline/CompetingDeadline vs. the portal's ClosesAt, so the service compares it.
        return _context.AssemblyMotions
            .Where(m => m.Status == AssemblyMotionStatus.CollectingCoSubmitters && m.DeletedAt == null)
            .ToList();
    }

    public List<AssemblyMotion> ListForMember(Guid portalId, Guid memberId) {
        var submittedMotionIds = _context.AssemblyMotionSubmitters
            .Where(s => s.MemberId == memberId)
            .Select(s => s.MotionId)
            .ToList();

        return _context.AssemblyMotions
            .Where(m => m.PortalId == portalId && m.DeletedAt == null
                && (m.CreatedByMemberId == memberId || submittedMotionIds.Contains(m.Id)))
            .ToList();
    }

    public void SoftDelete(Guid id, DateTime nowUtc)
        => _context.AssemblyMotions
            .Where(m => m.Id == id)
            .Set(m => m.DeletedAt, nowUtc)
            .Update();

    public void MarkWikiPending(Guid motionId, DateTime nowUtc)
        => _context.AssemblyMotions
            .Where(m => m.Id == motionId)
            .Set(m => m.WikiSyncState, WikiSyncState.Pending)
            .Set(m => m.WikiSyncRequestedAt, nowUtc)
            .Set(m => m.WikiNextAttemptAt, (DateTime?)null)
            .Set(m => m.WikiFailureCount, 0)
            .Update();
}
