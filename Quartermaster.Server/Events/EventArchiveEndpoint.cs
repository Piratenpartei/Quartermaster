using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.Events;

public class EventArchiveRequest {
    public Guid Id { get; set; }
}

public class EventArchiveEndpoint : Endpoint<EventArchiveRequest> {
    private readonly EventRepository _eventRepo;

    public EventArchiveEndpoint(EventRepository eventRepo) {
        _eventRepo = eventRepo;
    }

    public override void Configure() {
        Post("/api/events/{Id}/archive");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EventArchiveRequest req, CancellationToken ct) {
        var ev = _eventRepo.Get(req.Id);
        if (ev == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        _eventRepo.SetArchived(ev.Id, !ev.IsArchived);
        await SendOkAsync(ct);
    }
}
