using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Api.I18n;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Events;

public class ChecklistItemCheckEndpoint : Endpoint<ChecklistItemCheckRequest> {
    private readonly EventRepository _eventRepo;
    private readonly ChecklistItemExecutor _executor;
    private readonly PermissionContext _perms;

    public ChecklistItemCheckEndpoint(EventRepository eventRepo, ChecklistItemExecutor executor,
        PermissionContext perms) {
        _eventRepo = eventRepo;
        _executor = executor;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/events/{EventId}/checklist/{ItemId}/check");
    }

    public override async Task HandleAsync(ChecklistItemCheckRequest req, CancellationToken ct) {
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

        if (item.IsCompleted) {
            ThrowError(I18nKey.Error.Event.Checklist.AlreadyCompleted);
            return;
        }

        Guid? resultId = null;

        if (req.ExecuteAction && item.ItemType != ChecklistItemType.Text) {
            var parentEvent = _eventRepo.Get(item.EventId);
            var (execResultId, error) = await _executor.ExecuteAsync(item, parentEvent);
            if (error != null) {
                ThrowError(error);
                return;
            }
            resultId = execResultId;
        }

        _eventRepo.CheckItem(req.ItemId, resultId);
        _eventRepo.RefreshStatus(req.EventId);

        await SendOkAsync(ct);
    }
}
