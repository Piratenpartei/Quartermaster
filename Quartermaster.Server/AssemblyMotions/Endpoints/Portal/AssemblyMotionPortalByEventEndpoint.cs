using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Portal;

public class AssemblyMotionPortalByEventEndpoint : Endpoint<AssemblyMotionPortalByEventRequest, AssemblyMotionPortalDTO> {
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly EventRepository _events;
    private readonly PermissionContext _perms;

    public AssemblyMotionPortalByEventEndpoint(AssemblyMotionPortalRepository portals, EventRepository events, PermissionContext perms) {
        _portals = portals;
        _events = events;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/assemblymotions/portals/by-event/{EventId}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(AssemblyMotionPortalByEventRequest req, CancellationToken ct) {
        var portal = _portals.GetByEvent(req.EventId);
        var ev = portal == null ? null : _events.Get(req.EventId);
        if (portal == null || ev == null || !AssemblyMotionEventVisibility.IsVisible(ev, _perms)) {
            await SendNotFoundAsync(ct);
            return;
        }

        var dto = portal.ToDto(ev.PublicName, ev.EventDate, _portals.GetTypes(portal.Id));
        await SendAsync(dto, cancellation: ct);
    }
}
