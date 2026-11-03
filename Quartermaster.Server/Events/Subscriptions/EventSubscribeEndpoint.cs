using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.I18n;
using Quartermaster.Data.Events;
using Quartermaster.Server.AssemblyMotions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Events.Subscriptions;

public class EventSubscribeEndpoint : Endpoint<EventSubscriptionRequest> {
    private readonly EventRepository _eventRepo;
    private readonly EventSubscriptionRepository _subscriptions;
    private readonly PermissionContext _perms;

    public EventSubscribeEndpoint(EventRepository eventRepo, EventSubscriptionRepository subscriptions, PermissionContext perms) {
        _eventRepo = eventRepo;
        _subscriptions = subscriptions;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/events/{Id}/subscription");
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
        if (!EventSubscriptionRules.CanSubscribe(ev)) {
            AddError(I18nKey.Error.Event.SubscriptionClosed);
            await SendErrorsAsync(409, ct);
            return;
        }

        _subscriptions.Subscribe(ev.Id, _perms.UserId.Value, DateTime.UtcNow);
        await SendNoContentAsync(ct);
    }
}
