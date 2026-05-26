using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Events;

public class EventArchiveRequest {
    public Guid Id { get; set; }
}

public class EventArchiveEndpoint : Endpoint<EventArchiveRequest> {
    private readonly EventRepository _eventRepo;
    private readonly PermissionContext _perms;

    public EventArchiveEndpoint(EventRepository eventRepo, PermissionContext perms) {
        _eventRepo = eventRepo;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/events/{Id}/archive");
    }

    public override async Task HandleAsync(EventArchiveRequest req, CancellationToken ct) {
        var ev = _eventRepo.Get(req.Id);
        if (ev == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.Has(ev.ChapterId, PermissionIdentifier.DeleteEvents)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var newStatus = ev.Status == EventStatus.Archived ? EventStatus.Completed : EventStatus.Archived;
        _eventRepo.SetStatus(ev.Id, newStatus);
        await SendOkAsync(ct);
    }
}
