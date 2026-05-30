using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Events;

public class EventUpdateEndpoint : Endpoint<EventUpdateRequest> {
    private readonly EventRepository _eventRepo;
    private readonly PermissionContext _perms;

    public EventUpdateEndpoint(EventRepository eventRepo, PermissionContext perms) {
        _eventRepo = eventRepo;
        _perms = perms;
    }

    public override void Configure() {
        Put("/api/events/{Id}");
    }

    public override async Task HandleAsync(EventUpdateRequest req, CancellationToken ct) {
        var existing = _eventRepo.Get(req.Id);
        if (existing == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.Has(existing.ChapterId, PermissionIdentifier.EditEvents)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var ev = new Event {
            Id = req.Id,
            InternalName = req.InternalName,
            PublicName = req.PublicName,
            Description = req.Description,
            EventDate = req.EventDate.ToStorage(),
            Visibility = req.Visibility
        };

        _eventRepo.Update(ev);
        await SendOkAsync(ct);
    }
}
