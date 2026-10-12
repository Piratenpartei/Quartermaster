using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;
using Quartermaster.Server.AssemblyMotions.Wiki;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Portal;

public class AssemblyMotionWikiValidateEndpoint : Endpoint<AssemblyMotionPortalIdRequest, AssemblyMotionConnectionCheckDTO> {
    private const string FailurePrefix = "Anmeldung am Wiki fehlgeschlagen: ";

    private readonly AssemblyMotionPortalRepository _portals;
    private readonly EventRepository _events;
    private readonly AssemblyMotionAccess _access;
    private readonly AssemblyMotionWikiConnectionCheck _check;

    public AssemblyMotionWikiValidateEndpoint(AssemblyMotionPortalRepository portals, EventRepository events,
        AssemblyMotionAccess access, AssemblyMotionWikiConnectionCheck check) {
        _portals = portals;
        _events = events;
        _access = access;
        _check = check;
    }

    public override void Configure() {
        Post("/api/assemblymotions/portals/{PortalId}/wiki/validate");
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

        var result = await _check.CheckAsync(ct);
        var message = result.Ok || !result.Configured ? result.Message : FailurePrefix + result.Message;
        await SendAsync(new AssemblyMotionConnectionCheckDTO { Ok = result.Ok, Message = message }, cancellation: ct);
    }
}
