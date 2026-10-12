using System;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;
using Quartermaster.Data.Members;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.AssemblyMotions;

public class AssemblyMotionMemberActions {
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly AssemblyMotionRepository _motions;
    private readonly EventRepository _events;
    private readonly AssemblyMotionEligibility _eligibility;
    private readonly AssemblyMotionDetailBuilder _detailBuilder;
    private readonly AssemblyMotionAccess _access;
    private readonly PermissionContext _perms;

    public AssemblyMotionMemberActions(AssemblyMotionPortalRepository portals, AssemblyMotionRepository motions,
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

    public AssemblyMotionActionResult Create(Guid portalId, Func<AssemblyMotionPortal, Member, Guid, DateTime, AssemblyMotionOutcome> create) {
        var portal = _portals.Get(portalId);
        var ev = portal == null ? null : _events.Get(portal.EventId);
        if (portal == null || ev == null || !AssemblyMotionEventVisibility.IsVisible(ev, _perms))
            return AssemblyMotionActionResult.NotFound;

        var nowUtc = DateTime.UtcNow;
        var eligibility = _eligibility.Check(_perms.UserId, ev, nowUtc);
        if (!eligibility.IsEligible || _perms.UserId == null)
            return AssemblyMotionActionResult.Fail(403, I18nKey.Error.AssemblyMotion.NotEligible);

        var outcome = create(portal, eligibility.Member!, _perms.UserId.Value, nowUtc);
        if (!outcome.Succeeded)
            return AssemblyMotionActionResult.Fail(outcome.StatusCode, outcome.ErrorKey!);

        return BuildResult(outcome.Motion!.Id, portal, ev, eligibility, nowUtc);
    }

    public AssemblyMotionActionResult Run(Guid motionId, bool requiresEligibility, Func<AssemblyMotion, Member, Guid, DateTime, AssemblyMotionOutcome> action) {
        var motion = _motions.Get(motionId);
        var portal = motion == null ? null : _portals.Get(motion.PortalId);
        var ev = portal == null ? null : _events.Get(portal.EventId);
        if (motion == null || portal == null || ev == null || !AssemblyMotionEventVisibility.IsVisible(ev, _perms))
            return AssemblyMotionActionResult.NotFound;

        var nowUtc = DateTime.UtcNow;
        var eligibility = _eligibility.Check(_perms.UserId, ev, nowUtc);
        if (!_detailBuilder.CanSee(motion, ev, eligibility))
            return AssemblyMotionActionResult.NotFound;
        if (eligibility.Member == null || _perms.UserId == null)
            return AssemblyMotionActionResult.Fail(403, I18nKey.Error.AssemblyMotion.NotEligible);
        if (requiresEligibility && !eligibility.IsEligible)
            return AssemblyMotionActionResult.Fail(403, I18nKey.Error.AssemblyMotion.NotEligible);

        var outcome = action(motion, eligibility.Member, _perms.UserId.Value, nowUtc);
        if (!outcome.Succeeded)
            return AssemblyMotionActionResult.Fail(outcome.StatusCode, outcome.ErrorKey!);

        return BuildResult(motionId, portal, ev, eligibility, nowUtc);
    }

    /// <summary>
    /// Runs <paramref name="action"/> for a caller who is either the commission of the motion's event or a member;
    /// the action receives whether the caller acts as commission and decides the remaining rules itself.
    /// </summary>
    public AssemblyMotionActionResult RunAsCommissionOrMember(Guid motionId, bool requiresEligibility,
        Func<AssemblyMotion, Member?, Guid, bool, DateTime, AssemblyMotionOutcome> action) {
        var motion = _motions.Get(motionId);
        var portal = motion == null ? null : _portals.Get(motion.PortalId);
        var ev = portal == null ? null : _events.Get(portal.EventId);
        if (motion == null || portal == null || ev == null || !AssemblyMotionEventVisibility.IsVisible(ev, _perms))
            return AssemblyMotionActionResult.NotFound;

        var nowUtc = DateTime.UtcNow;
        var eligibility = _eligibility.Check(_perms.UserId, ev, nowUtc);
        if (!_detailBuilder.CanSee(motion, ev, eligibility))
            return AssemblyMotionActionResult.NotFound;

        var asCommission = _access.IsCommission(ev);
        if (_perms.UserId == null || (eligibility.Member == null && !asCommission))
            return AssemblyMotionActionResult.Fail(403, I18nKey.Error.AssemblyMotion.NotEligible);
        if (!asCommission && requiresEligibility && !eligibility.IsEligible)
            return AssemblyMotionActionResult.Fail(403, I18nKey.Error.AssemblyMotion.NotEligible);

        var outcome = action(motion, eligibility.Member, _perms.UserId.Value, asCommission, nowUtc);
        if (!outcome.Succeeded)
            return AssemblyMotionActionResult.Fail(outcome.StatusCode, outcome.ErrorKey!);

        return BuildResult(motionId, portal, ev, eligibility, nowUtc);
    }

    private AssemblyMotionActionResult BuildResult(Guid motionId, AssemblyMotionPortal portal, Event ev,
        EligibilityResult eligibility, DateTime nowUtc) {
        var motion = _motions.Get(motionId);
        var type = motion == null ? null : _portals.GetType(motion.TypeId);
        if (motion == null || type == null)
            return AssemblyMotionActionResult.Done;

        var detail = _detailBuilder.Build(motion, portal, type, ev, eligibility, nowUtc);
        return detail == null ? AssemblyMotionActionResult.Done : AssemblyMotionActionResult.Ok(detail);
    }
}
