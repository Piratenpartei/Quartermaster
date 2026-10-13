using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Members;

public class AssemblyMotionRevisionsEndpoint : Endpoint<AssemblyMotionIdRequest, List<AssemblyMotionRevisionDTO>> {
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly AssemblyMotionRepository _motions;
    private readonly EventRepository _events;
    private readonly AssemblyMotionEligibility _eligibility;
    private readonly AssemblyMotionAccess _access;
    private readonly PermissionContext _perms;

    public AssemblyMotionRevisionsEndpoint(AssemblyMotionPortalRepository portals, AssemblyMotionRepository motions,
        EventRepository events, AssemblyMotionEligibility eligibility, AssemblyMotionAccess access, PermissionContext perms) {
        _portals = portals;
        _motions = motions;
        _events = events;
        _eligibility = eligibility;
        _access = access;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/assemblymotions/motions/{Id}/revisions");
    }

    public override async Task HandleAsync(AssemblyMotionIdRequest req, CancellationToken ct) {
        var motion = _motions.Get(req.Id);
        var portal = motion == null ? null : _portals.Get(motion.PortalId);
        var ev = portal == null ? null : _events.Get(portal.EventId);
        if (motion == null || portal == null || ev == null || !AssemblyMotionEventVisibility.IsVisible(ev, _perms)) {
            await SendNotFoundAsync(ct);
            return;
        }

        var eligibility = _eligibility.Check(_perms.UserId, ev, DateTime.UtcNow);
        var isCommission = _access.IsCommission(ev);
        var isSubmitter = eligibility.Member != null && _motions.GetSubmitters(motion.Id).Any(s => s.MemberId == eligibility.Member.Id);

        var viewer = new AssemblyMotionViewer(eligibility.Member?.Id, eligibility.IsEligible, isCommission);
        if (!AssemblyMotionVisibility.CanSee(motion, viewer)) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (motion.Status != AssemblyMotionStatus.Draft && !eligibility.IsEligible && !isSubmitter && !isCommission) {
            AddError(I18nKey.Error.AssemblyMotion.NotEligible);
            await SendErrorsAsync(403, ct);
            return;
        }

        var revisions = _motions.GetRevisions(motion.Id);
        var isAuthor = eligibility.Member != null && motion.CreatedByMemberId == eligibility.Member.Id;
        if (!isAuthor) {
            // Revisions saved while the motion was a draft stay private to its author.
            var firstReleasedRevision = _motions.GetReleasedRevision(motion.Id) ?? motion.Revision;
            revisions = revisions
                .Where(r => r.Revision >= firstReleasedRevision)
                .ToList();
        }

        var dtos = revisions
            .Select(r => r.ToDto())
            .ToList();
        await SendAsync(dtos, cancellation: ct);
    }
}
