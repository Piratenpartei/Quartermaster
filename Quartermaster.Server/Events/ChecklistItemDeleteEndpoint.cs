using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Events;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.Events;

public class ChecklistItemDeleteEndpoint : Endpoint<ChecklistItemDeleteRequest> {
    private readonly EventRepository _eventRepo;

    public ChecklistItemDeleteEndpoint(EventRepository eventRepo) {
        _eventRepo = eventRepo;
    }

    public override void Configure() {
        Delete("/api/events/{EventId}/checklist/{ItemId}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ChecklistItemDeleteRequest req, CancellationToken ct) {
        _eventRepo.DeleteChecklistItem(req.ItemId);
        await SendOkAsync(ct);
    }
}
