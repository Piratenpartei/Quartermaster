using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Data;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Events.Subscriptions;

/// <summary>Always allowed for an existing event, also when it is completed, deleted or no longer visible, so nobody stays subscribed against their will.</summary>
public class EventUnsubscribeEndpoint : Endpoint<EventSubscriptionRequest> {
    private readonly DbContext _db;
    private readonly EventSubscriptionRepository _subscriptions;
    private readonly PermissionContext _perms;

    public EventUnsubscribeEndpoint(DbContext db, EventSubscriptionRepository subscriptions, PermissionContext perms) {
        _db = db;
        _subscriptions = subscriptions;
        _perms = perms;
    }

    public override void Configure() {
        Delete("/api/events/{Id}/subscription");
    }

    public override async Task HandleAsync(EventSubscriptionRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_db.Events.Any(e => e.Id == req.Id)) {
            await SendNotFoundAsync(ct);
            return;
        }

        _subscriptions.Unsubscribe(req.Id, _perms.UserId.Value);
        await SendNoContentAsync(ct);
    }
}
