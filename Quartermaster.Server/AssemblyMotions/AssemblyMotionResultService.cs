using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.AssemblyMotions;

public class AssemblyMotionResultService {
    private const string EntityType = "AssemblyMotion";

    private readonly AssemblyMotionRepository _motions;
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly EventRepository _events;
    private readonly AuditLogRepository _auditLog;

    public AssemblyMotionResultService(AssemblyMotionRepository motions, AssemblyMotionPortalRepository portals,
        EventRepository events, AuditLogRepository auditLog) {
        _motions = motions;
        _portals = portals;
        _events = events;
        _auditLog = auditLog;
    }

    public static bool IsEventReached(DateTime? eventDate, DateTime nowUtc) {
        if (eventDate == null)
            return false;
        return BerlinTime.FromUtc(nowUtc).Date >= BerlinTime.FromUtc(eventDate.Value).Date;
    }

    public AssemblyMotionOutcome SetResult(Guid motionId, AssemblyMotionResult result, DateTime nowUtc) {
        if (!Enum.IsDefined(result))
            return AssemblyMotionOutcome.Fail(422, I18nKey.Error.AssemblyMotion.InvalidState);

        using var tx = _motions.BeginTransaction();

        var motion = _motions.GetForUpdate(motionId);
        if (motion == null)
            return AssemblyMotionOutcome.Fail(404, I18nKey.Error.AssemblyMotion.NotFound);

        var problem = EventProblem(motion.PortalId, nowUtc);
        if (problem != null)
            return problem;
        if (motion.Number == null)
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.InvalidState);

        Apply(motion, result, nowUtc);

        tx.Commit();
        return AssemblyMotionOutcome.Ok(motion);
    }

    public AssemblyMotionOutcome SetResults(Guid portalId, IReadOnlyCollection<AssemblyMotionResultItem> items, DateTime nowUtc) {
        if (items.Any(i => !Enum.IsDefined(i.Result)))
            return AssemblyMotionOutcome.Fail(422, I18nKey.Error.AssemblyMotion.InvalidState);

        var resultByMotion = items
            .GroupBy(i => i.MotionId)
            .ToDictionary(g => g.Key, g => g.Last().Result);

        using var tx = _motions.BeginTransaction();

        var problem = EventProblem(portalId, nowUtc);
        if (problem != null)
            return problem;

        var locked = new List<AssemblyMotion>();
        foreach (var motionId in resultByMotion.Keys.Order()) {
            var motion = _motions.GetForUpdate(motionId);
            if (!IsBulkTarget(motion, portalId))
                return AssemblyMotionOutcome.Fail(404, I18nKey.Error.AssemblyMotion.NotFound);
            if (motion.Number == null)
                return AssemblyMotionOutcome.Fail(409, I18nParams.With(I18nKey.Error.AssemblyMotion.ResultItemInvalid, ("number", motion.Title)));

            locked.Add(motion);
        }

        foreach (var motion in locked)
            Apply(motion, resultByMotion[motion.Id], nowUtc);

        tx.Commit();
        return new AssemblyMotionOutcome(200, null, null);
    }

    private static bool IsBulkTarget([NotNullWhen(true)] AssemblyMotion? motion, Guid portalId) {
        if (motion == null)
            return false;
        if (motion.PortalId != portalId)
            return false;

        return motion.Status != AssemblyMotionStatus.Draft;
    }

    private AssemblyMotionOutcome? EventProblem(Guid portalId, DateTime nowUtc) {
        var portal = _portals.Get(portalId)
            ?? throw new InvalidOperationException($"Portal {portalId} not found.");
        var ev = _events.Get(portal.EventId)
            ?? throw new InvalidOperationException($"Event {portal.EventId} not found for portal {portalId}.");

        if (!IsEventReached(ev.EventDate, nowUtc))
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.EventNotReached);

        return null;
    }

    private void Apply(AssemblyMotion motion, AssemblyMotionResult result, DateTime nowUtc) {
        if (motion.Result == result)
            return;

        var before = motion.Result;
        motion.Result = result;
        _motions.Update(motion);
        _motions.MarkWikiPending(motion.Id, nowUtc);

        _auditLog.LogFieldChange(EntityType, motion.Id, nameof(AssemblyMotion.Result), before.ToString(), result.ToString());
    }
}
