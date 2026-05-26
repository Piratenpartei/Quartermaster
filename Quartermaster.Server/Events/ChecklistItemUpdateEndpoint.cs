using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Events;

public class ChecklistItemUpdateEndpoint : Endpoint<ChecklistItemUpdateRequest> {
    private readonly EventRepository _eventRepo;
    private readonly PermissionContext _perms;

    public ChecklistItemUpdateEndpoint(EventRepository eventRepo, PermissionContext perms) {
        _eventRepo = eventRepo;
        _perms = perms;
    }

    public override void Configure() {
        Put("/api/events/{EventId}/checklist/{ItemId}");
    }

    public override async Task HandleAsync(ChecklistItemUpdateRequest req, CancellationToken ct) {
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
            Id = req.ItemId,
            EventId = req.EventId,
            SortOrder = req.SortOrder,
            ItemType = req.ItemType,
            Label = req.Label,
            Configuration = req.Configuration != null ? EventConfigSerializer.Serialize(req.Configuration) : null
        };

        _eventRepo.UpdateChecklistItem(item);

        await SendOkAsync(ct);
    }
}
