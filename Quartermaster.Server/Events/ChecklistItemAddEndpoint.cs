using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Events;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.Events;

public class ChecklistItemAddEndpoint : Endpoint<ChecklistItemCreateRequest, EventChecklistItemDTO> {
    private readonly EventRepository _eventRepo;

    public ChecklistItemAddEndpoint(EventRepository eventRepo) {
        _eventRepo = eventRepo;
    }

    public override void Configure() {
        Post("/api/events/{EventId}/checklist");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ChecklistItemCreateRequest req, CancellationToken ct) {
        var item = new EventChecklistItem {
            EventId = req.EventId,
            SortOrder = req.SortOrder,
            ItemType = (ChecklistItemType)req.ItemType,
            Label = req.Label,
            Configuration = req.Configuration
        };

        _eventRepo.CreateChecklistItem(item);

        await SendAsync(new EventChecklistItemDTO {
            Id = item.Id,
            SortOrder = item.SortOrder,
            ItemType = req.ItemType,
            Label = item.Label,
            IsCompleted = false,
            CompletedAt = null,
            Configuration = item.Configuration,
            ResultId = null
        }, cancellation: ct);
    }
}
