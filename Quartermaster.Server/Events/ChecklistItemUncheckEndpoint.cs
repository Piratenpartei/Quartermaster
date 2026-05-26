using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Api.I18n;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Events;

public class ChecklistItemUncheckEndpoint : Endpoint<ChecklistItemUncheckRequest> {
    private readonly EventRepository _eventRepo;
    private readonly PermissionContext _perms;

    public ChecklistItemUncheckEndpoint(EventRepository eventRepo, PermissionContext perms) {
        _eventRepo = eventRepo;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/events/{EventId}/checklist/{ItemId}/uncheck");
    }

    public override async Task HandleAsync(ChecklistItemUncheckRequest req, CancellationToken ct) {
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

        var item = _eventRepo.GetChecklistItem(req.ItemId);
        if (item == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (item.ItemType != ChecklistItemType.Text) {
            ThrowError(I18nKey.Error.Event.Checklist.OnlyTextCanBeUnchecked);
            return;
        }

        _eventRepo.UncheckItem(req.ItemId);

        await SendOkAsync(ct);
    }
}
