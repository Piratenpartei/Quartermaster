using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Portal;

public class AssemblyMotionPortalSettingsEndpoint : Endpoint<AssemblyMotionPortalSettingsRequest, AssemblyMotionPortalSettingsDTO> {
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly EventRepository _events;
    private readonly AssemblyMotionAccess _access;
    private readonly AssemblyMotionPortalSettingsBuilder _builder;

    public AssemblyMotionPortalSettingsEndpoint(AssemblyMotionPortalRepository portals, EventRepository events,
        AssemblyMotionAccess access, AssemblyMotionPortalSettingsBuilder builder) {
        _portals = portals;
        _events = events;
        _access = access;
        _builder = builder;
    }

    public override void Configure() {
        Get("/api/assemblymotions/portals/{Id}/settings");
    }

    public override async Task HandleAsync(AssemblyMotionPortalSettingsRequest req, CancellationToken ct) {
        var portal = _portals.Get(req.Id);
        var ev = portal == null ? null : _events.Get(portal.EventId);
        if (portal == null || ev == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!_access.CanEditPortal(ev)) {
            await SendForbiddenAsync(ct);
            return;
        }

        await SendAsync(_builder.Build(portal, ev), cancellation: ct);
    }
}
