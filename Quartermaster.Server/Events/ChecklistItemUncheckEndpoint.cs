using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Events;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.Events;

public class ChecklistItemUncheckEndpoint : Endpoint<ChecklistItemUncheckRequest> {
    private readonly EventRepository _eventRepo;

    public ChecklistItemUncheckEndpoint(EventRepository eventRepo) {
        _eventRepo = eventRepo;
    }

    public override void Configure() {
        Post("/api/events/{EventId}/checklist/{ItemId}/uncheck");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ChecklistItemUncheckRequest req, CancellationToken ct) {
        var item = _eventRepo.GetChecklistItem(req.ItemId);
        if (item == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (item.ItemType != ChecklistItemType.Text) {
            ThrowError("Only text items can be unchecked.");
            return;
        }

        _eventRepo.UncheckItem(req.ItemId);

        await SendOkAsync(ct);
    }
}
