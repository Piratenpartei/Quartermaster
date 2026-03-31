using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Events;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.Events;

public class ChecklistItemUpdateEndpoint : Endpoint<ChecklistItemUpdateRequest> {
    private readonly EventRepository _eventRepo;

    public ChecklistItemUpdateEndpoint(EventRepository eventRepo) {
        _eventRepo = eventRepo;
    }

    public override void Configure() {
        Put("/api/events/{EventId}/checklist/{ItemId}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ChecklistItemUpdateRequest req, CancellationToken ct) {
        var item = new EventChecklistItem {
            Id = req.ItemId,
            EventId = req.EventId,
            SortOrder = req.SortOrder,
            ItemType = (ChecklistItemType)req.ItemType,
            Label = req.Label,
            Configuration = req.Configuration
        };

        _eventRepo.UpdateChecklistItem(item);

        await SendOkAsync(ct);
    }
}
