using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Portal;

public class AssemblyMotionTypeDeleteEndpoint : Endpoint<AssemblyMotionTypeDeleteRequest> {
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly EventRepository _events;
    private readonly AssemblyMotionAccess _access;

    public AssemblyMotionTypeDeleteEndpoint(AssemblyMotionPortalRepository portals, EventRepository events, AssemblyMotionAccess access) {
        _portals = portals;
        _events = events;
        _access = access;
    }

    public override void Configure() {
        Delete("/api/assemblymotions/types/{Id}");
    }

    public override async Task HandleAsync(AssemblyMotionTypeDeleteRequest req, CancellationToken ct) {
        var type = _portals.GetType(req.Id);
        var portal = type == null ? null : _portals.Get(type.PortalId);
        var ev = portal == null ? null : _events.Get(portal.EventId);
        if (type == null || ev == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!_access.CanEditPortal(ev)) {
            await SendForbiddenAsync(ct);
            return;
        }
        if (_portals.TypeHasMotions(type.Id)) {
            AddError(I18nKey.Error.AssemblyMotion.Type.HasMotions);
            await SendErrorsAsync(409, ct);
            return;
        }

        _portals.DeleteType(type.Id);
        await SendNoContentAsync(ct);
    }
}
