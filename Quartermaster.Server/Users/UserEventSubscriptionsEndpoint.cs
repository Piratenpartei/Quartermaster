using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Events;
using Quartermaster.Data;
using Quartermaster.Data.Events;
using Quartermaster.Server.AssemblyMotions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Users;

/// <summary>The caller's event subscriptions, by date with undated events last; events the caller can no longer see appear without name and date.</summary>
public class UserEventSubscriptionsEndpoint : EndpointWithoutRequest<List<EventSubscriptionListItemDTO>> {
    private readonly EventSubscriptionRepository _subscriptions;
    private readonly DbContext _db;
    private readonly PermissionContext _perms;

    public UserEventSubscriptionsEndpoint(EventSubscriptionRepository subscriptions, DbContext db, PermissionContext perms) {
        _subscriptions = subscriptions;
        _db = db;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/users/event-subscriptions");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var eventIds = _subscriptions.ListForUser(_perms.UserId.Value)
            .Select(s => s.EventId)
            .ToList();
        var items = _db.Events
            .Where(e => eventIds.Contains(e.Id) && e.DeletedAt == null)
            .ToList()
            .OrderBy(e => e.EventDate == null)
            .ThenBy(e => e.EventDate)
            .ThenBy(e => e.StartTime)
            .ThenBy(e => e.PublicName)
            .Select(e => e.ToSubscriptionListItemDto(AssemblyMotionEventVisibility.IsVisible(e, _perms)))
            .ToList();

        await SendAsync(items, cancellation: ct);
    }
}
