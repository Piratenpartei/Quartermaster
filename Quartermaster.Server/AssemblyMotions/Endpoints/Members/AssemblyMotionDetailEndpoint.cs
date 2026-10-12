using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Members;

public class AssemblyMotionDetailEndpoint : Endpoint<AssemblyMotionIdRequest, AssemblyMotionDetailDTO> {
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly AssemblyMotionRepository _motions;
    private readonly EventRepository _events;
    private readonly AssemblyMotionEligibility _eligibility;
    private readonly AssemblyMotionDetailBuilder _detailBuilder;
    private readonly PermissionContext _perms;

    public AssemblyMotionDetailEndpoint(AssemblyMotionPortalRepository portals, AssemblyMotionRepository motions,
        EventRepository events, AssemblyMotionEligibility eligibility, AssemblyMotionDetailBuilder detailBuilder,
        PermissionContext perms) {
        _portals = portals;
        _motions = motions;
        _events = events;
        _eligibility = eligibility;
        _detailBuilder = detailBuilder;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/assemblymotions/motions/{Id}");
    }

    public override async Task HandleAsync(AssemblyMotionIdRequest req, CancellationToken ct) {
        var motion = _motions.Get(req.Id);
        var portal = motion == null ? null : _portals.Get(motion.PortalId);
        var type = motion == null ? null : _portals.GetType(motion.TypeId);
        var ev = portal == null ? null : _events.Get(portal.EventId);
        if (motion == null || portal == null || type == null || ev == null || !AssemblyMotionEventVisibility.IsVisible(ev, _perms)) {
            await SendNotFoundAsync(ct);
            return;
        }

        var nowUtc = DateTime.UtcNow;
        var eligibility = _eligibility.Check(_perms.UserId, ev, nowUtc);
        var detail = _detailBuilder.Build(motion, portal, type, ev, eligibility, nowUtc);
        if (detail == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        await SendAsync(detail, cancellation: ct);
    }
}
