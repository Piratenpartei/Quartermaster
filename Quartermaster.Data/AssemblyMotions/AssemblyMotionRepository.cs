using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using LinqToDB;
using LinqToDB.Data;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.Events;
using Quartermaster.Data.AuditLog;

namespace Quartermaster.Data.AssemblyMotions;

public class AssemblyMotionRepository {
    public const int WikiLastErrorMaxLength = 4000;
    public const string ReleasedAuditAction = "Released";
    public const string ReleasedRevisionField = "Revision";

    private readonly DbContext _context;
    private readonly AuditLogRepository _auditLog;

    public AssemblyMotionRepository(DbContext context, AuditLogRepository auditLog) {
        _context = context;
        _auditLog = auditLog;
    }

    // READ COMMITTED so reads after a lock wait see rows committed meanwhile (e.g. the numbers handed out under LockType); REPEATABLE READ would keep the earlier snapshot.
    public DataConnectionTransaction BeginTransaction() {
        return _context.BeginTransaction(IsolationLevel.ReadCommitted);
    }

    public AssemblyMotion? Get(Guid id) {
        return _context.AssemblyMotions.Where(m => m.Id == id && m.DeletedAt == null).FirstOrDefault();
    }

    public AssemblyMotion? GetForUpdate(Guid id) {
        return _context.Query<AssemblyMotion>(
            $"SELECT * FROM {AssemblyMotion.TableName} WHERE Id = @id AND DeletedAt IS NULL FOR UPDATE",
            new DataParameter("id", id)).FirstOrDefault();
    }

    public void LockType(Guid typeId) {
        _context.Query<Guid>(
                $"SELECT Id FROM {AssemblyMotionType.TableName} WHERE Id = @id FOR UPDATE",
                new DataParameter("id", typeId)).FirstOrDefault();
    }

    public int NextNumber(Guid portalId, Guid typeId) {
        var maxNumber = _context.AssemblyMotions
            .Where(m => m.PortalId == portalId && m.TypeId == typeId && m.Number != null)
            .Select(m => m.Number)
            .Max();

        return (maxNumber ?? 0) + 1;
    }

    public int CountDrafts(Guid portalId, Guid memberId) {
        return _context.AssemblyMotions.Count(m => m.PortalId == portalId && m.CreatedByMemberId == memberId
                && m.Status == AssemblyMotionStatus.Draft && m.DeletedAt == null);
    }

    public void Insert(AssemblyMotion motion) {
        _context.Insert(motion);
    }

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

    /// <summary>Writes only the competing flag, leaving every other column untouched.</summary>
    public void SetCompetingFlag(Guid motionId, bool value) {
        _context.AssemblyMotions
                .Where(m => m.Id == motionId)
                .Set(m => m.IsCompetingMotion, value)
                .Update();
    }

    public List<AssemblyMotionSubmitter> GetSubmitters(Guid motionId) {
        return _context.AssemblyMotionSubmitters
                .Where(s => s.MotionId == motionId)
                .OrderByDescending(s => s.IsAuthor)
                .ThenBy(s => s.JoinedAt)
                .ToList();
    }

    public Dictionary<Guid, List<AssemblyMotionSubmitter>> GetSubmitters(IReadOnlyCollection<Guid> motionIds) {
        return _context.AssemblyMotionSubmitters
            .Where(s => motionIds.Contains(s.MotionId))
            .OrderByDescending(s => s.IsAuthor)
            .ThenBy(s => s.JoinedAt)
            .ToList()
            .GroupBy(s => s.MotionId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    public void AddSubmitter(AssemblyMotionSubmitter submitter) {
        _context.Insert(submitter);
    }

    public void RemoveSubmitter(Guid motionId, Guid memberId) {
        _context.AssemblyMotionSubmitters
                .Where(s => s.MotionId == motionId && s.MemberId == memberId)
                .Delete();
    }

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

    public void AddRevision(AssemblyMotionRevision revision) {
        _context.Insert(revision);
    }

    public List<AssemblyMotionRevision> GetRevisions(Guid motionId) {
        return _context.AssemblyMotionRevisions
                .Where(r => r.MotionId == motionId)
                .OrderBy(r => r.Revision)
                .ToList();
    }

    /// <summary>
    /// The revision that was current when the motion was first released, read from the audit entry written by the release. Null when no such entry carries a revision.
    /// </summary>
    public int? GetReleasedRevision(Guid motionId) {
        var value = _context.AuditLogs
            .Where(a => a.EntityType == "AssemblyMotion" && a.EntityId == motionId
                && a.Action == ReleasedAuditAction && a.FieldName == ReleasedRevisionField)
            .OrderBy(a => a.Timestamp)
            .Select(a => a.NewValue)
            .FirstOrDefault();

        return int.TryParse(value, out var revision) ? revision : null;
    }

    public AssemblyMotionRevision? GetRevision(Guid motionId, int revision) {
        return _context.AssemblyMotionRevisions
                .Where(r => r.MotionId == motionId && r.Revision == revision)
                .FirstOrDefault();
    }

    /// <summary>Full rows of the numbered motions of the portal (drafts never have a number), for callers that need the texts.</summary>
    public List<AssemblyMotion> ListNumberedByPortal(Guid portalId) {
        return _context.AssemblyMotions
                .Where(m => m.PortalId == portalId && m.DeletedAt == null && m.Number != null)
                .ToList();
    }

    public List<Guid> ListNumberedIds(Guid portalId) {
        return _context.AssemblyMotions
                .Where(m => m.PortalId == portalId && m.DeletedAt == null && m.Number != null)
                .Select(m => m.Id)
                .ToList();
    }

    /// <summary>
    /// The non-draft motions of the portal for list and ranking views, optionally of one status. Drafts are excluded in SQL and only the columns the lists need are read: the Markdown and HTML texts, summary, keywords, check note and the wiki and OpenSlides error texts are left at their defaults and must not be used.
    /// </summary>
    public List<AssemblyMotion> ListSummariesByPortal(Guid portalId, AssemblyMotionStatus? status = null) {
        var query = _context.AssemblyMotions
            .Where(m => m.PortalId == portalId && m.DeletedAt == null && m.Status != AssemblyMotionStatus.Draft);
        if (status != null)
            query = query.Where(m => m.Status == status);

        return query
            .Select(m => new AssemblyMotion {
                Id = m.Id,
                PortalId = m.PortalId,
                TypeId = m.TypeId,
                Number = m.Number,
                Title = m.Title,
                IsCompetingMotion = m.IsCompetingMotion,
                Status = m.Status,
                Result = m.Result,
                Revision = m.Revision,
                CreatedByMemberId = m.CreatedByMemberId,
                CreatedAt = m.CreatedAt,
                SubmittedAt = m.SubmittedAt,
                WikiSyncState = m.WikiSyncState,
                ForumSyncState = m.ForumSyncState,
                OpenSlidesPushedRevision = m.OpenSlidesPushedRevision
            })
            .ToList();
    }

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

    public void SoftDelete(Guid id, DateTime nowUtc) {
        _context.AssemblyMotions
                .Where(m => m.Id == id)
                .Set(m => m.DeletedAt, nowUtc)
                .Update();
    }

    /// <summary>Queues the motion for the wiki and the forum sync, each only while the motion's portal mirrors there.</summary>
    public void MarkMirrorsPending(Guid motionId, DateTime nowUtc) {
        MarkWikiPending(motionId, nowUtc);
        MarkForumPending(motionId, nowUtc);
    }

    /// <summary>Queues the motion for the wiki sync. Does nothing while the motion's portal has the wiki mirror disabled.</summary>
    public void MarkWikiPending(Guid motionId, DateTime nowUtc) {
        _context.AssemblyMotions
                .Where(m => m.Id == motionId
                    && _context.AssemblyMotionPortals.Any(p => p.Id == m.PortalId && p.WikiMirrorEnabled))
                .Set(m => m.WikiSyncState, WikiSyncState.Pending)
                .Set(m => m.WikiSyncRequestedAt, nowUtc)
                .Set(m => m.WikiNextAttemptAt, (DateTime?)null)
                .Set(m => m.WikiFailureCount, 0)
                .Update();
    }

    /// <summary>
    /// Queues the motion like <see cref="MarkWikiPending"/> and forgets that it was ever written to the wiki (hash and revision cleared), so the next sync treats the page as a first write: an existing page is taken over only when it carries the motion's portal URL, and the page is protected.
    /// </summary>
    public void MarkWikiPendingAsNeverSynced(Guid motionId, DateTime nowUtc) {
        _context.AssemblyMotions
                .Where(m => m.Id == motionId
                    && _context.AssemblyMotionPortals.Any(p => p.Id == m.PortalId && p.WikiMirrorEnabled))
                .Set(m => m.WikiSyncState, WikiSyncState.Pending)
                .Set(m => m.WikiSyncRequestedAt, nowUtc)
                .Set(m => m.WikiNextAttemptAt, (DateTime?)null)
                .Set(m => m.WikiFailureCount, 0)
                .Set(m => m.WikiSyncedHash, (string?)null)
                .Set(m => m.WikiSyncedRevision, (int?)null)
                .Update();
        MarkForumPending(motionId, nowUtc);
    }

    /// <summary>Queues the motion for the forum sync and returns whether it was queued. Does nothing while the motion's portal has the forum mirror disabled or the motion's topic was deleted in the forum.</summary>
    public bool MarkForumPending(Guid motionId, DateTime nowUtc) {
        return _context.AssemblyMotions
                .Where(m => m.Id == motionId
                    && m.ForumSyncState != ForumSyncState.Deleted
                    && _context.AssemblyMotionPortals.Any(p => p.Id == m.PortalId && p.ForumMirrorEnabled))
                .Set(m => m.ForumSyncState, ForumSyncState.Pending)
                .Set(m => m.ForumSyncRequestedAt, nowUtc)
                .Set(m => m.ForumNextAttemptAt, (DateTime?)null)
                .Set(m => m.ForumFailureCount, 0)
                .Update() > 0;
    }

    public void ResetForumPendingToNotMirrored(Guid portalId) {
        _context.AssemblyMotions
                .Where(m => m.PortalId == portalId && m.ForumSyncState == ForumSyncState.Pending)
                .Set(m => m.ForumSyncState, ForumSyncState.NotMirrored)
                .Update();
    }

    /// <summary>Numbered motions of forum-mirrored portals whose topic needs work now, oldest request first. A motion that was never mirrored counts as due.</summary>
    public List<AssemblyMotion> ListForumDue(DateTime nowUtc, int limit) {
        return _context.AssemblyMotions
            .InnerJoin(_context.AssemblyMotionPortals, (m, p) => m.PortalId == p.Id, (m, p) => new { Motion = m, Portal = p })
            .Where(x => x.Portal.ForumMirrorEnabled
                && x.Motion.Number != null
                && x.Motion.DeletedAt == null
                && (x.Motion.ForumSyncState == ForumSyncState.Pending
                    || x.Motion.ForumSyncState == ForumSyncState.NotMirrored
                    || (x.Motion.ForumSyncState == ForumSyncState.Failed && x.Motion.ForumNextAttemptAt <= nowUtc)))
            .OrderBy(x => x.Motion.ForumSyncRequestedAt)
            .Select(x => x.Motion)
            .Take(limit)
            .ToList();
    }

    public void SetForumTopic(Guid motionId, int topicId, int postId) {
        _context.AssemblyMotions
                .Where(m => m.Id == motionId)
                .Set(m => m.ForumTopicId, (int?)topicId)
                .Set(m => m.ForumPostId, (int?)postId)
                .Update();
    }

    /// <summary>
    /// Records a finished forum sync like <see cref="CompleteWikiSync"/>: the state only changes while the request timestamp is still the loaded one, otherwise only the facts about the written post are stored and false is returned.
    /// </summary>
    public bool CompleteForumSync(Guid motionId, DateTime? loadedRequestedAt, int revision, string hash, DateTime nowUtc) {
        var applied = _context.AssemblyMotions
            .Where(m => m.Id == motionId && m.ForumSyncRequestedAt == loadedRequestedAt)
            .Set(m => m.ForumSyncState, ForumSyncState.Synced)
            .Set(m => m.ForumSyncedRevision, revision)
            .Set(m => m.ForumSyncedHash, hash)
            .Set(m => m.ForumLastSyncedAt, nowUtc)
            .Set(m => m.ForumLastError, (string?)null)
            .Set(m => m.ForumFailureCount, 0)
            .Set(m => m.ForumNextAttemptAt, (DateTime?)null)
            .Update() > 0;

        if (applied)
            return true;

        _context.AssemblyMotions
            .Where(m => m.Id == motionId)
            .Set(m => m.ForumSyncedRevision, revision)
            .Set(m => m.ForumSyncedHash, hash)
            .Set(m => m.ForumLastSyncedAt, nowUtc)
            .Update();
        return false;
    }

    /// <summary>Records a failed forum sync like <see cref="FailWikiSync"/>.</summary>
    public bool FailForumSync(Guid motionId, DateTime? loadedRequestedAt, string error, int failureCount, DateTime nextAttemptAt) {
        var truncated = TruncateError(error);
        var applied = _context.AssemblyMotions
            .Where(m => m.Id == motionId && m.ForumSyncRequestedAt == loadedRequestedAt)
            .Set(m => m.ForumSyncState, ForumSyncState.Failed)
            .Set(m => m.ForumLastError, truncated)
            .Set(m => m.ForumFailureCount, failureCount)
            .Set(m => m.ForumNextAttemptAt, (DateTime?)nextAttemptAt)
            .Update() > 0;

        if (applied)
            return true;

        _context.AssemblyMotions
            .Where(m => m.Id == motionId)
            .Set(m => m.ForumLastError, truncated)
            .Update();
        return false;
    }

    /// <summary>Marks the motion's topic as deleted in the forum; the motion is never synced or queued again.</summary>
    public void MarkForumDeleted(Guid motionId, string error) {
        _context.AssemblyMotions
                .Where(m => m.Id == motionId)
                .Set(m => m.ForumSyncState, ForumSyncState.Deleted)
                .Set(m => m.ForumLastError, TruncateError(error))
                .Set(m => m.ForumNextAttemptAt, (DateTime?)null)
                .Update();
    }

    /// <summary>Sets the pending motions of the portal back to not mirrored; synced and failed motions keep their state as history.</summary>
    public void ResetWikiPendingToNotMirrored(Guid portalId) {
        _context.AssemblyMotions
                .Where(m => m.PortalId == portalId && m.WikiSyncState == WikiSyncState.Pending)
                .Set(m => m.WikiSyncState, WikiSyncState.NotMirrored)
                .Update();
    }

    /// <summary>Numbered motions of mirrored portals whose wiki page needs work now, oldest request first. A motion that was never mirrored (portal mirroring enabled later) counts as due.</summary>
    public List<AssemblyMotion> ListWikiDue(DateTime nowUtc, int limit) {
        return _context.AssemblyMotions
            .InnerJoin(_context.AssemblyMotionPortals, (m, p) => m.PortalId == p.Id, (m, p) => new { Motion = m, Portal = p })
            .Where(x => x.Portal.WikiMirrorEnabled
                && x.Motion.Number != null
                && x.Motion.DeletedAt == null
                && (x.Motion.WikiSyncState == WikiSyncState.Pending
                    || x.Motion.WikiSyncState == WikiSyncState.NotMirrored
                    || ((x.Motion.WikiSyncState == WikiSyncState.Failed || x.Motion.WikiSyncState == WikiSyncState.ProtectionFailed)
                        && x.Motion.WikiNextAttemptAt <= nowUtc)))
            .OrderBy(x => x.Motion.WikiSyncRequestedAt)
            .Select(x => x.Motion)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// A window of at most <paramref name="batchSize"/> synced, numbered motions of mirrored portals whose event is not archived, ordered by id. The window start rotates by day (days since 2000-01-01 times the batch size, modulo the total, wrapping at the end) so repeated daily calls cover every motion over time.
    /// </summary>
    public List<AssemblyMotion> ListWikiSynced(DateTime nowUtc, int batchSize) {
        var query = _context.AssemblyMotions
            .InnerJoin(_context.AssemblyMotionPortals, (m, p) => m.PortalId == p.Id, (m, p) => new { Motion = m, Portal = p })
            .InnerJoin(_context.Events, (x, e) => x.Portal.EventId == e.Id, (x, e) => new { x.Motion, x.Portal, Event = e })
            .Where(x => x.Portal.WikiMirrorEnabled
                && x.Event.Status != EventStatus.Archived
                && x.Motion.Number != null
                && x.Motion.DeletedAt == null
                && x.Motion.WikiSyncState == WikiSyncState.Synced)
            .Select(x => x.Motion)
            .OrderBy(m => m.Id);

        var total = query.Count();
        if (total <= batchSize)
            return query.ToList();

        var dayIndex = (long)(nowUtc.Date - new DateTime(2000, 1, 1)).TotalDays;
        var offset = (int)(dayIndex * batchSize % total);
        var window = query.Skip(offset).Take(batchSize).ToList();
        if (window.Count < batchSize)
            window.AddRange(query.Take(batchSize - window.Count).ToList());

        return window;
    }

    /// <summary>
    /// Records a finished sync with one conditional UPDATE that only matches while the request timestamp is still the one the sync started with. Returns false when a newer change arrived meanwhile: state, error and request stay untouched so the motion remains pending, and only the facts about the page that was written (revision, hash, time) are stored.
    /// </summary>
    public bool CompleteWikiSync(Guid motionId, DateTime? loadedRequestedAt, WikiSyncState state, int revision, string hash, string? lastError, DateTime? nextAttemptAt, DateTime nowUtc) {
        var applied = _context.AssemblyMotions
            .Where(m => m.Id == motionId && m.WikiSyncRequestedAt == loadedRequestedAt)
            .Set(m => m.WikiSyncState, state)
            .Set(m => m.WikiSyncedRevision, revision)
            .Set(m => m.WikiSyncedHash, hash)
            .Set(m => m.WikiLastSyncedAt, nowUtc)
            .Set(m => m.WikiLastError, TruncateError(lastError))
            .Set(m => m.WikiFailureCount, 0)
            .Set(m => m.WikiNextAttemptAt, nextAttemptAt)
            .Update() > 0;

        if (applied)
            return true;

        _context.AssemblyMotions
            .Where(m => m.Id == motionId)
            .Set(m => m.WikiSyncedRevision, revision)
            .Set(m => m.WikiSyncedHash, hash)
            .Set(m => m.WikiLastSyncedAt, nowUtc)
            .Update();
        return false;
    }

    /// <summary>
    /// Records a failed sync with one conditional UPDATE. When a newer change arrived meanwhile the state stays pending and the backoff is not applied (the newer request reset it); only the error text is stored. Returns whether the failure state was applied.
    /// </summary>
    public bool FailWikiSync(Guid motionId, DateTime? loadedRequestedAt, string error, int failureCount, DateTime nextAttemptAt) {
        var truncated = TruncateError(error);
        var applied = _context.AssemblyMotions
            .Where(m => m.Id == motionId && m.WikiSyncRequestedAt == loadedRequestedAt)
            .Set(m => m.WikiSyncState, WikiSyncState.Failed)
            .Set(m => m.WikiLastError, truncated)
            .Set(m => m.WikiFailureCount, failureCount)
            .Set(m => m.WikiNextAttemptAt, nextAttemptAt)
            .Update() > 0;

        if (applied)
            return true;

        _context.AssemblyMotions
            .Where(m => m.Id == motionId)
            .Set(m => m.WikiLastError, truncated)
            .Update();
        return false;
    }

    private static string? TruncateError(string? error) {
        return error != null && error.Length > WikiLastErrorMaxLength ? error[..WikiLastErrorMaxLength] : error;
    }
}
