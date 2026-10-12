using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Members;

public class AssemblyMotionPortalContextEndpoint : Endpoint<AssemblyMotionPortalIdRequest, AssemblyMotionPortalContextDTO> {
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly EventRepository _events;
    private readonly AssemblyMotionEligibility _eligibility;
    private readonly AssemblyMotionAccess _access;
    private readonly PermissionContext _perms;

    public AssemblyMotionPortalContextEndpoint(AssemblyMotionPortalRepository portals, EventRepository events,
        AssemblyMotionEligibility eligibility, AssemblyMotionAccess access, PermissionContext perms) {
        _portals = portals;
        _events = events;
        _eligibility = eligibility;
        _access = access;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/assemblymotions/portals/{PortalId}/context");
        AllowAnonymous();
    }

    public override async Task HandleAsync(AssemblyMotionPortalIdRequest req, CancellationToken ct) {
        var portal = _portals.Get(req.PortalId);
        var ev = portal == null ? null : _events.Get(portal.EventId);
        if (portal == null || ev == null || !AssemblyMotionEventVisibility.IsVisible(ev, _perms)) {
            await SendNotFoundAsync(ct);
            return;
        }

        var eligibility = _eligibility.Check(_perms.UserId, ev, DateTime.UtcNow);
        await SendAsync(new AssemblyMotionPortalContextDTO {
            IsSignedIn = _perms.UserId != null,
            IsEligible = eligibility.IsEligible,
            Problem = eligibility.Problem,
            IsCommission = _access.IsCommission(ev),
            CanEditPortal = _access.CanEditPortal(ev)
        }, cancellation: ct);
    }
}
