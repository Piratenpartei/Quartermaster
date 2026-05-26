using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Events;

public class ChecklistItemReorderRequest {
    public Guid EventId { get; set; }
    public Guid ItemId { get; set; }
    public int Direction { get; set; }
}

public class ChecklistItemReorderEndpoint : Endpoint<ChecklistItemReorderRequest> {
    private readonly EventRepository _eventRepo;
    private readonly PermissionContext _perms;

    public ChecklistItemReorderEndpoint(EventRepository eventRepo, PermissionContext perms) {
        _eventRepo = eventRepo;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/events/{EventId}/checklist/{ItemId}/reorder");
    }

    public override async Task HandleAsync(ChecklistItemReorderRequest req, CancellationToken ct) {
        var ev = _eventRepo.Get(req.EventId);
        if (ev == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.Has(ev.ChapterId, PermissionIdentifier.EditEvents)) {
            await SendForbiddenAsync(ct);
            return;
        }

        _eventRepo.SwapChecklistItemOrder(req.EventId, req.ItemId, req.Direction);
        await SendOkAsync(ct);
    }
}
