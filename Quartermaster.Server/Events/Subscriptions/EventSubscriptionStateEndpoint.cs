using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Events;
using Quartermaster.Data.Events;
using Quartermaster.Server.AssemblyMotions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Events.Subscriptions;

public class EventSubscriptionStateEndpoint : Endpoint<EventSubscriptionRequest, EventSubscriptionStateDTO> {
    private readonly EventRepository _eventRepo;
    private readonly EventSubscriptionRepository _subscriptions;
    private readonly PermissionContext _perms;

    public EventSubscriptionStateEndpoint(EventRepository eventRepo, EventSubscriptionRepository subscriptions, PermissionContext perms) {
        _eventRepo = eventRepo;
        _subscriptions = subscriptions;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/events/{Id}/subscription");
    }

    public override async Task HandleAsync(EventSubscriptionRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var ev = _eventRepo.Get(req.Id);
        if (ev == null || ev.DeletedAt != null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!AssemblyMotionEventVisibility.IsVisible(ev, _perms)) {
            await SendForbiddenAsync(ct);
            return;
        }

        await SendAsync(new EventSubscriptionStateDTO {
            IsSubscribed = _subscriptions.IsSubscribed(ev.Id, _perms.UserId.Value),
            CanSubscribe = EventSubscriptionRules.CanSubscribe(ev)
        }, cancellation: ct);
    }
}
