using System;
using System.Linq;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.AuditLog;

namespace Quartermaster.Server.AssemblyMotions.Wiki;

/// <summary>Queues motions for the wiki sync job. Never talks to the wiki itself.</summary>
public class AssemblyMotionWikiResync {
    public const string AuditAction = "WikiResyncRequested";

    private readonly AssemblyMotionRepository _motions;
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly AuditLogRepository _auditLog;

    public AssemblyMotionWikiResync(AssemblyMotionRepository motions, AssemblyMotionPortalRepository portals, AuditLogRepository auditLog) {
        _motions = motions;
        _portals = portals;
        _auditLog = auditLog;
    }

    /// <summary>Resets the state, the failure count and the backoff of one numbered motion so the next job tick picks it up.</summary>
    public AssemblyMotionOutcome QueueOne(Guid motionId, DateTime nowUtc) {
        var motion = _motions.Get(motionId);
        var portal = motion == null ? null : _portals.Get(motion.PortalId);
        if (motion == null || portal == null)
            return AssemblyMotionOutcome.Fail(404, I18nKey.Error.AssemblyMotion.NotFound);
        if (!portal.WikiMirrorEnabled)
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.WikiMirrorDisabled);
        if (motion.Number == null)
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.WikiMotionNotNumbered);

        _motions.MarkWikiPending(motion.Id, nowUtc);
        _auditLog.Log("AssemblyMotion", motion.Id, AuditAction);
        return AssemblyMotionOutcome.Ok(motion);
    }

    /// <summary>Queues every numbered motion of the portal and returns how many; writes one audit entry for the portal.</summary>
    public int QueueAll(Guid portalId, DateTime nowUtc) {
        var count = QueueNumbered(portalId, nowUtc);
        _auditLog.Log("AssemblyMotionPortal", portalId, AuditAction, "Count", null, count.ToString());
        return count;
    }

    public void ResetPending(Guid portalId) => _motions.ResetWikiPendingToNotMirrored(portalId);

    /// <summary>Queues every numbered motion of the portal without an audit entry, e.g. when the mirror has just been enabled.</summary>
    public int QueueNumbered(Guid portalId, DateTime nowUtc) {
        var ids = _motions.ListByPortal(portalId)
            .Where(m => m.Number != null)
            .Select(m => m.Id)
            .ToList();

        foreach (var id in ids)
            _motions.MarkWikiPending(id, nowUtc);

        return ids.Count;
    }
}
