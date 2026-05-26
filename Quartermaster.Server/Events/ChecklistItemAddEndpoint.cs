using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Events;

public class ChecklistItemAddEndpoint : Endpoint<ChecklistItemCreateRequest, EventChecklistItemDTO> {
    private readonly EventRepository _eventRepo;
    private readonly PermissionContext _perms;

    public ChecklistItemAddEndpoint(EventRepository eventRepo, PermissionContext perms) {
        _eventRepo = eventRepo;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/events/{EventId}/checklist");
    }

    public override async Task HandleAsync(ChecklistItemCreateRequest req, CancellationToken ct) {
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

        var item = new EventChecklistItem {
            EventId = req.EventId,
            SortOrder = req.SortOrder,
            ItemType = req.ItemType,
            Label = req.Label,
            Configuration = req.Configuration != null ? EventConfigSerializer.Serialize(req.Configuration) : null
        };

        _eventRepo.CreateChecklistItem(item);

        await SendAsync(new EventChecklistItemDTO {
            Id = item.Id,
            SortOrder = item.SortOrder,
            ItemType = req.ItemType,
            Label = item.Label,
            IsCompleted = false,
            CompletedAt = null,
            Configuration = req.Configuration,
            ResultId = null
        }, cancellation: ct);
    }
}
