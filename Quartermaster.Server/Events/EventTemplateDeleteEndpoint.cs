using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Events;

public class EventTemplateDeleteRequest {
    public Guid Id { get; set; }
}

public class EventTemplateDeleteEndpoint : Endpoint<EventTemplateDeleteRequest> {
    private readonly EventRepository _eventRepo;
    private readonly PermissionContext _perms;

    public EventTemplateDeleteEndpoint(EventRepository eventRepo, PermissionContext perms) {
        _eventRepo = eventRepo;
        _perms = perms;
    }

    public override void Configure() {
        Delete("/api/eventtemplates/{Id}");
    }

    public override async Task HandleAsync(EventTemplateDeleteRequest req, CancellationToken ct) {
        var template = _eventRepo.GetTemplate(req.Id);
        if (template == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        if (template.ChapterId.HasValue) {
            if (!_perms.Has(template.ChapterId.Value, PermissionIdentifier.EditTemplates)) {
                await SendForbiddenAsync(ct);
                return;
            }
        } else {
            if (!_perms.HasGlobal(PermissionIdentifier.EditTemplates)) {
                await SendForbiddenAsync(ct);
                return;
            }
        }

        _eventRepo.DeleteTemplate(req.Id);
        await SendOkAsync(ct);
    }
}
