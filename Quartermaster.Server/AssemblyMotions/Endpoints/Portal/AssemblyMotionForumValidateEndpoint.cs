using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;
using Quartermaster.Server.AssemblyMotions.Forum;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Portal;

public class AssemblyMotionForumValidateEndpoint : Endpoint<AssemblyMotionForumValidateRequest, AssemblyMotionConnectionCheckDTO> {
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly EventRepository _events;
    private readonly AssemblyMotionAccess _access;
    private readonly AssemblyMotionForumConnectionCheck _check;

    public AssemblyMotionForumValidateEndpoint(AssemblyMotionPortalRepository portals, EventRepository events,
        AssemblyMotionAccess access, AssemblyMotionForumConnectionCheck check) {
        _portals = portals;
        _events = events;
        _access = access;
        _check = check;
    }

    public override void Configure() {
        Post("/api/assemblymotions/portals/{PortalId}/forum/validate");
    }

    public override async Task HandleAsync(AssemblyMotionForumValidateRequest req, CancellationToken ct) {
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

        var result = await _check.CheckAsync(req.CategoryId, ct);
        await SendAsync(new AssemblyMotionConnectionCheckDTO {
            Ok = result.Ok,
            Message = result.Message,
            Warnings = result.Warnings.ToList()
        }, cancellation: ct);
    }
}
