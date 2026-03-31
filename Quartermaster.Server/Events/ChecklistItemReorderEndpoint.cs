using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.Events;

public class ChecklistItemReorderRequest {
    public Guid EventId { get; set; }
    public Guid ItemId { get; set; }
    public int Direction { get; set; }
}

public class ChecklistItemReorderEndpoint : Endpoint<ChecklistItemReorderRequest> {
    private readonly EventRepository _eventRepo;

    public ChecklistItemReorderEndpoint(EventRepository eventRepo) {
        _eventRepo = eventRepo;
    }

    public override void Configure() {
        Post("/api/events/{EventId}/checklist/{ItemId}/reorder");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ChecklistItemReorderRequest req, CancellationToken ct) {
        _eventRepo.SwapChecklistItemOrder(req.EventId, req.ItemId, req.Direction);
        await SendOkAsync(ct);
    }
}
