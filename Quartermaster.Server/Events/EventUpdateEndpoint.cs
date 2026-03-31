using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Events;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.Events;

public class EventUpdateEndpoint : Endpoint<EventUpdateRequest> {
    private readonly EventRepository _eventRepo;

    public EventUpdateEndpoint(EventRepository eventRepo) {
        _eventRepo = eventRepo;
    }

    public override void Configure() {
        Put("/api/events/{Id}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EventUpdateRequest req, CancellationToken ct) {
        var existing = _eventRepo.Get(req.Id);
        if (existing == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var ev = new Event {
            Id = req.Id,
            InternalName = req.InternalName,
            PublicName = req.PublicName,
            Description = req.Description,
            EventDate = req.EventDate
        };

        _eventRepo.Update(ev);
        await SendOkAsync(ct);
    }
}
