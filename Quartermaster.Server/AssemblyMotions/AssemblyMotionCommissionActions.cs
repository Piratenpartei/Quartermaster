using System;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.AssemblyMotions;

public class AssemblyMotionCommissionActions {
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly AssemblyMotionRepository _motions;
    private readonly EventRepository _events;
    private readonly AssemblyMotionEligibility _eligibility;
    private readonly AssemblyMotionDetailBuilder _detailBuilder;
    private readonly AssemblyMotionAccess _access;
    private readonly PermissionContext _perms;

    public AssemblyMotionCommissionActions(AssemblyMotionPortalRepository portals, AssemblyMotionRepository motions,
        EventRepository events, AssemblyMotionEligibility eligibility, AssemblyMotionDetailBuilder detailBuilder,
        AssemblyMotionAccess access, PermissionContext perms) {
        _portals = portals;
        _motions = motions;
        _events = events;
        _eligibility = eligibility;
        _detailBuilder = detailBuilder;
        _access = access;
        _perms = perms;
    }

    public AssemblyMotionActionResult Run(Guid motionId, Func<Guid, DateTime, AssemblyMotionOutcome> action) {
        var motion = _motions.Get(motionId);
        var portal = motion == null ? null : _portals.Get(motion.PortalId);
        var ev = portal == null ? null : _events.Get(portal.EventId);
        if (motion == null || portal == null || ev == null || !AssemblyMotionEventVisibility.IsVisible(ev, _perms))
            return AssemblyMotionActionResult.NotFound;
        if (!_access.IsCommission(ev))
            return AssemblyMotionActionResult.Fail(403, I18nKey.Error.AssemblyMotion.NotCommission);
        if (!AssemblyMotionVisibility.IsPublic(motion) || _perms.UserId == null)
            return AssemblyMotionActionResult.NotFound;

        var nowUtc = DateTime.UtcNow;
        var outcome = action(_perms.UserId.Value, nowUtc);
        if (!outcome.Succeeded)
            return AssemblyMotionActionResult.Fail(outcome.StatusCode, outcome.ErrorKey!);

        var updated = _motions.Get(motionId);
        var type = updated == null ? null : _portals.GetType(updated.TypeId);
        if (updated == null || type == null)
            return AssemblyMotionActionResult.Done;

        var eligibility = _eligibility.Check(_perms.UserId, ev, nowUtc);
        var detail = _detailBuilder.Build(updated, portal, type, ev, eligibility, nowUtc);
        return detail == null ? AssemblyMotionActionResult.Done : AssemblyMotionActionResult.Ok(detail);
    }
}
