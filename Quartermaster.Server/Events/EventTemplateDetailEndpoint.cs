using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Events;

public class EventTemplateDetailRequest {
    public Guid Id { get; set; }
}

public class EventTemplateDetailEndpoint : Endpoint<EventTemplateDetailRequest, EventTemplateDetailDTO> {
    private readonly EventRepository _eventRepo;
    private readonly PermissionContext _perms;

    public EventTemplateDetailEndpoint(EventRepository eventRepo, PermissionContext perms) {
        _eventRepo = eventRepo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/eventtemplates/{Id}");
    }

    public override async Task HandleAsync(EventTemplateDetailRequest req, CancellationToken ct) {
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
            if (!_perms.Has(template.ChapterId.Value, PermissionIdentifier.ViewTemplates)) {
                await SendForbiddenAsync(ct);
                return;
            }
        } else {
            if (!_perms.HasGlobal(PermissionIdentifier.ViewTemplates)) {
                await SendForbiddenAsync(ct);
                return;
            }
        }

        await SendAsync(new EventTemplateDetailDTO {
            Id = template.Id,
            Name = template.Name,
            PublicNameTemplate = template.PublicNameTemplate,
            DescriptionTemplate = template.DescriptionTemplate,
            Variables = EventConfigSerializer.ParseVariables(template.Variables),
            ChecklistItemTemplates = EventConfigSerializer.ParseTemplates(template.ChecklistItemTemplates),
            ChapterId = template.ChapterId,
            CreatedAt = template.CreatedAt
        }, cancellation: ct);
    }
}
