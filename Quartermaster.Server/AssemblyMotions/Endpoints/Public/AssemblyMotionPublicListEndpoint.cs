using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Public;

public class AssemblyMotionPublicListEndpoint : Endpoint<AssemblyMotionPublicListRequest, AssemblyMotionPublicListResponse> {
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly AssemblyMotionRepository _motions;
    private readonly EventRepository _events;
    private readonly PermissionContext _perms;

    public AssemblyMotionPublicListEndpoint(
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
        Get("/api/assemblymotions/portals/{PortalId}/motions");
        AllowAnonymous();
    }

    public override async Task HandleAsync(AssemblyMotionPublicListRequest req, CancellationToken ct) {
        var portal = _portals.Get(req.PortalId);
        var ev = portal == null ? null : _events.Get(portal.EventId);
        if (portal == null || ev == null || !AssemblyMotionEventVisibility.IsVisible(ev, _perms)) {
            await SendNotFoundAsync(ct);
            return;
        }

        var types = _portals.GetTypes(portal.Id).ToDictionary(t => t.Id);
        var search = req.Search?.Trim();

        var filtered = _motions.ListSummariesByPortal(portal.Id)
            .Where(m => types.ContainsKey(m.TypeId))
            .Where(m => req.TypeId == null || m.TypeId == req.TypeId)
            .Where(m => req.Status == null || m.Status == req.Status)
            .Where(m => AssemblyMotionSearch.Matches(m, types[m.TypeId], search))
            .OrderBy(m => types[m.TypeId].SortOrder)
            .ThenBy(m => m.Number == null)
            .ThenBy(m => m.Number)
            .ThenBy(m => m.CreatedAt)
            .ToList();

        var items = filtered
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .Select(m => AssemblyMotionPublicView.BuildListItem(m, types[m.TypeId]))
            .ToList();

        await SendAsync(new AssemblyMotionPublicListResponse {
            Items = items,
            TotalCount = filtered.Count,
            Page = req.Page,
            PageSize = req.PageSize
        }, cancellation: ct);
    }
}
