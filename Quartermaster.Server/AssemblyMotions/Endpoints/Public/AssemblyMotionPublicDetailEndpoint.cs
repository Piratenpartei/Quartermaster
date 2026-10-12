using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Public;

public class AssemblyMotionPublicDetailEndpoint : Endpoint<AssemblyMotionPublicDetailRequest, AssemblyMotionPublicDTO> {
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly AssemblyMotionRepository _motions;
    private readonly EventRepository _events;
    private readonly PermissionContext _perms;

    public AssemblyMotionPublicDetailEndpoint(
        AssemblyMotionPortalRepository portals,
        AssemblyMotionRepository motions,
        EventRepository events,
        PermissionContext perms) {
        _portals = portals;
        _motions = motions;
        _events = events;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/assemblymotions/motions/{Id}/public");
        AllowAnonymous();
    }

    public override async Task HandleAsync(AssemblyMotionPublicDetailRequest req, CancellationToken ct) {
        var motion = _motions.Get(req.Id);
        var type = motion == null ? null : _portals.GetType(motion.TypeId);
        var portal = motion == null ? null : GetAccessiblePortal(motion);
        if (motion == null || type == null || portal == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var dto = AssemblyMotionPublicView.Build(motion, type);
        dto.PortalId = portal.Id;
        dto.EventId = portal.EventId;
        await SendAsync(dto, cancellation: ct);
    }

    private AssemblyMotionPortal? GetAccessiblePortal(AssemblyMotion motion) {
        if (!AssemblyMotionVisibility.IsPublic(motion))
            return null;

        var portal = _portals.Get(motion.PortalId);
        if (portal == null)
            return null;

        var ev = _events.Get(portal.EventId);
        if (ev == null)
            return null;

        return AssemblyMotionEventVisibility.IsVisible(ev, _perms) ? portal : null;
    }
}
