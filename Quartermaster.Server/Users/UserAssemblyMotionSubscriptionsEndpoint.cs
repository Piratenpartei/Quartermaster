using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using LinqToDB;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;
using Quartermaster.Server.AssemblyMotions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Users;

/// <summary>
/// The caller's motion subscriptions, by event date with undated events last, then by title. Deleted motions and motions of events
/// the caller can no longer see appear without title and event name.
/// </summary>
public class UserAssemblyMotionSubscriptionsEndpoint : EndpointWithoutRequest<List<AssemblyMotionSubscriptionListItemDTO>> {
    private readonly AssemblyMotionSubscriptionRepository _subscriptions;
    private readonly DbContext _db;
    private readonly PermissionContext _perms;

    public UserAssemblyMotionSubscriptionsEndpoint(AssemblyMotionSubscriptionRepository subscriptions, DbContext db, PermissionContext perms) {
        _subscriptions = subscriptions;
        _db = db;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/users/assembly-motion-subscriptions");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var motionIds = _subscriptions.ListForUser(_perms.UserId.Value)
            .Select(s => s.MotionId)
            .ToList();
        var rows = _db.AssemblyMotions
            .Where(m => motionIds.Contains(m.Id))
            .InnerJoin(_db.AssemblyMotionPortals, (m, p) => m.PortalId == p.Id, (m, p) => new { Motion = m, p.EventId })
            .InnerJoin(_db.Events, (x, e) => x.EventId == e.Id, (x, e) => new { x.Motion, Event = e })
            .ToList();
        var items = rows
            .OrderBy(x => x.Event.EventDate == null)
            .ThenBy(x => x.Event.EventDate)
            .ThenBy(x => x.Motion.Title)
            .Select(x => x.Motion.ToSubscriptionListItemDto(IsVisible(x.Motion, x.Event), x.Event.PublicName))
            .ToList();

        await SendAsync(items, cancellation: ct);
    }

    private bool IsVisible(AssemblyMotion motion, Event ev) {
        if (motion.DeletedAt != null || ev.DeletedAt != null)
            return false;
        return AssemblyMotionEventVisibility.IsVisible(ev, _perms);
    }
}
