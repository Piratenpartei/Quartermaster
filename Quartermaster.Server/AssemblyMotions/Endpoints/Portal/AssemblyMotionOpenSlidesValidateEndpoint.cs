using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;
using Quartermaster.Server.AssemblyMotions.OpenSlides;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Portal;

public class AssemblyMotionOpenSlidesValidateEndpoint : Endpoint<AssemblyMotionPortalIdRequest, AssemblyMotionConnectionCheckDTO> {
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly EventRepository _events;
    private readonly AssemblyMotionAccess _access;
    private readonly AssemblyMotionOpenSlidesPush _push;

    public AssemblyMotionOpenSlidesValidateEndpoint(AssemblyMotionPortalRepository portals, EventRepository events,
        AssemblyMotionAccess access, AssemblyMotionOpenSlidesPush push) {
        _portals = portals;
        _events = events;
        _access = access;
        _push = push;
    }

    public override void Configure() {
        Post("/api/assemblymotions/portals/{PortalId}/openslides/validate");
    }

    public override async Task HandleAsync(AssemblyMotionPortalIdRequest req, CancellationToken ct) {
        var portal = _portals.Get(req.PortalId);
        var ev = portal == null ? null : _events.Get(portal.EventId);
        if (portal == null || ev == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!_access.CanEditPortal(ev)) {
            await SendForbiddenAsync(ct);
            return;
        }

        await SendAsync(await _push.ValidateAsync(ct), cancellation: ct);
    }
}
