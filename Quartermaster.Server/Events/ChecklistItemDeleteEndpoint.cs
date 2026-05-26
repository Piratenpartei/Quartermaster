using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Events;

public class ChecklistItemDeleteEndpoint : Endpoint<ChecklistItemDeleteRequest> {
    private readonly EventRepository _eventRepo;
    private readonly PermissionContext _perms;

    public ChecklistItemDeleteEndpoint(EventRepository eventRepo, PermissionContext perms) {
        _eventRepo = eventRepo;
        _perms = perms;
    }

    public override void Configure() {
        Delete("/api/events/{EventId}/checklist/{ItemId}");
    }

    public override async Task HandleAsync(ChecklistItemDeleteRequest req, CancellationToken ct) {
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

        _eventRepo.DeleteChecklistItem(req.ItemId);
        await SendOkAsync(ct);
    }
}
