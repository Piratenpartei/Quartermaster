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
    private readonly EventTemplateAccess _templateAccess;

    public EventTemplateDeleteEndpoint(EventRepository eventRepo, PermissionContext perms, EventTemplateAccess templateAccess) {
        _eventRepo = eventRepo;
        _perms = perms;
        _templateAccess = templateAccess;
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

        if (!_templateAccess.CanEdit(template)) {
            await SendForbiddenAsync(ct);
            return;
        }

        _eventRepo.DeleteTemplate(req.Id);
        await SendOkAsync(ct);
    }
}
