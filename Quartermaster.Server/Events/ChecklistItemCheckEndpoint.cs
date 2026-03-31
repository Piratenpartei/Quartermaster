using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Events;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.Events;

public class ChecklistItemCheckEndpoint : Endpoint<ChecklistItemCheckRequest> {
    private readonly EventRepository _eventRepo;
    private readonly ChecklistItemExecutor _executor;

    public ChecklistItemCheckEndpoint(EventRepository eventRepo, ChecklistItemExecutor executor) {
        _eventRepo = eventRepo;
        _executor = executor;
    }

    public override void Configure() {
        Post("/api/events/{EventId}/checklist/{ItemId}/check");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ChecklistItemCheckRequest req, CancellationToken ct) {
        var item = _eventRepo.GetChecklistItem(req.ItemId);
        if (item == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (item.IsCompleted) {
            ThrowError("Item is already completed.");
            return;
        }

        Guid? resultId = null;

        if (req.ExecuteAction && item.ItemType != ChecklistItemType.Text) {
            var parentEvent = _eventRepo.Get(item.EventId);
            var (execResultId, error) = _executor.Execute(item, parentEvent);
            if (error != null) {
                ThrowError(error);
                return;
            }
            resultId = execResultId;
        }

        _eventRepo.CheckItem(req.ItemId, resultId);

        await SendOkAsync(ct);
    }
}
