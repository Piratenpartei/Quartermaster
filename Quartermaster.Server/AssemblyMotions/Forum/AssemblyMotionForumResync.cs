using System;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.AuditLog;

namespace Quartermaster.Server.AssemblyMotions.Forum;

/// <summary>Queues motions for the forum sync job. Never talks to the forum itself.</summary>
public class AssemblyMotionForumResync {
    public const string AuditAction = "ForumResyncRequested";

    private readonly AssemblyMotionRepository _motions;
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly AuditLogRepository _auditLog;

    public AssemblyMotionForumResync(AssemblyMotionRepository motions, AssemblyMotionPortalRepository portals, AuditLogRepository auditLog) {
        _motions = motions;
        _portals = portals;
        _auditLog = auditLog;
    }

    /// <summary>Resets the state, the failure count and the backoff of one numbered motion so the next job tick picks it up. A topic deleted in the forum stays deleted.</summary>
    public AssemblyMotionOutcome QueueOne(Guid motionId, DateTime nowUtc) {
        var motion = _motions.Get(motionId);
        var portal = motion == null ? null : _portals.Get(motion.PortalId);
        if (motion == null || portal == null)
            return AssemblyMotionOutcome.Fail(404, I18nKey.Error.AssemblyMotion.NotFound);
        if (!portal.ForumMirrorEnabled)
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.ForumMirrorDisabled);
        if (motion.Status == AssemblyMotionStatus.Draft)
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.ForumMotionNotReleased);
        if (motion.ForumSyncState == ForumSyncState.Deleted)
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.ForumTopicDeleted);

        _motions.MarkForumPending(motion.Id, nowUtc);
        _auditLog.Log("AssemblyMotion", motion.Id, AuditAction);
        return AssemblyMotionOutcome.Ok(motion);
    }

    /// <summary>Queues every motion of the portal the forum mirrors whose topic was not deleted and returns how many; writes one audit entry for the portal.</summary>
    public int QueueAll(Guid portalId, DateTime nowUtc) {
        var count = QueueReleased(portalId, nowUtc);
        _auditLog.Log("AssemblyMotionPortal", portalId, AuditAction, "Count", null, count.ToString());
        return count;
    }

    public int QueueReleased(Guid portalId, DateTime nowUtc) {
        var count = 0;
        foreach (var id in _motions.ListReleasedIds(portalId)) {
            if (_motions.MarkForumPending(id, nowUtc))
                count++;
        }

        return count;
    }

    public void ResetPending(Guid portalId) {
        _motions.ResetForumPendingToNotMirrored(portalId);
    }
}
