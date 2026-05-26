using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Api.I18n;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Events;

public class EventStatusUpdateEndpoint : Endpoint<EventStatusUpdateRequest> {
    private readonly EventRepository _eventRepo;
    private readonly PermissionContext _perms;

    public EventStatusUpdateEndpoint(EventRepository eventRepo, PermissionContext perms) {
        _eventRepo = eventRepo;
        _perms = perms;
    }

    public override void Configure() {
        Put("/api/events/{Id}/status");
    }

    public override async Task HandleAsync(EventStatusUpdateRequest req, CancellationToken ct) {
        var ev = _eventRepo.Get(req.Id);
        if (ev == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var requiredPerm = req.Status == EventStatus.Archived || ev.Status == EventStatus.Archived
            ? PermissionIdentifier.DeleteEvents
            : PermissionIdentifier.EditEvents;

        if (!_perms.Has(ev.ChapterId, requiredPerm)) {
            await SendForbiddenAsync(ct);
            return;
        }

        if (!IsTransitionAllowed(ev.Status, req.Status)) {
            ThrowError(I18nParams.With(I18nKey.Error.Event.Status.TransitionInvalid,
                ("from", ev.Status.ToString()),
                ("to", req.Status.ToString())));
            return;
        }

        _eventRepo.SetStatus(req.Id, req.Status);
        await SendOkAsync(ct);
    }

    private static bool IsTransitionAllowed(EventStatus from, EventStatus to) {
        if (from == to)
            return false;

        return (from, to) switch {
            (EventStatus.Draft, EventStatus.Active) => true,
            (EventStatus.Active, EventStatus.Draft) => true,
            (EventStatus.Active, EventStatus.Completed) => true,
            (EventStatus.Completed, EventStatus.Active) => true,
            (EventStatus.Completed, EventStatus.Archived) => true,
            (EventStatus.Archived, EventStatus.Completed) => true,
            _ => false
        };
    }
}
