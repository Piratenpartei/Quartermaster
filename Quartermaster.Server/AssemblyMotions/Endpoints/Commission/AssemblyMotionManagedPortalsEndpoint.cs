using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Commission;

public class AssemblyMotionManagedPortalsEndpoint : EndpointWithoutRequest<List<AssemblyMotionManagedPortalDTO>> {
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly EventRepository _events;
    private readonly AssemblyMotionAccess _access;
    private readonly DbContext _db;

    public AssemblyMotionManagedPortalsEndpoint(AssemblyMotionPortalRepository portals, EventRepository events,
        AssemblyMotionAccess access, DbContext db) {
        _portals = portals;
        _events = events;
        _access = access;
        _db = db;
    }

    public override void Configure() {
        Get("/api/assemblymotions/portals/managed");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var chapterNames = _db.Chapters
            .ToList()
            .ToDictionary(c => c.Id, c => c.Name);

        var managed = _portals.GetForChapters(null)
            .Select(portal => new { Portal = portal, Event = _events.Get(portal.EventId) })
            .Where(x => x.Event != null && _access.IsCommission(x.Event))
            .Select(x => new AssemblyMotionManagedPortalDTO {
                PortalId = x.Portal.Id,
                EventId = x.Event!.Id,
                EventName = x.Event.PublicName,
                EventDate = x.Event.EventDate.ToDtoDate(),
                ChapterName = chapterNames.GetValueOrDefault(x.Event.ChapterId, "")
            })
            .OrderBy(x => x.EventDate == null)
            .ThenBy(x => x.EventDate)
            .ThenBy(x => x.EventName)
            .ToList();

        await SendAsync(managed, cancellation: ct);
    }
}
