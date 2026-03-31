using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Events;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.Events;

public class EventTemplateDetailRequest {
    public Guid Id { get; set; }
}

public class EventTemplateDetailEndpoint : Endpoint<EventTemplateDetailRequest, EventTemplateDetailDTO> {
    private readonly EventRepository _eventRepo;

    public EventTemplateDetailEndpoint(EventRepository eventRepo) {
        _eventRepo = eventRepo;
    }

    public override void Configure() {
        Get("/api/eventtemplates/{Id}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EventTemplateDetailRequest req, CancellationToken ct) {
        var template = _eventRepo.GetTemplate(req.Id);
        if (template == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        await SendAsync(new EventTemplateDetailDTO {
            Id = template.Id,
            Name = template.Name,
            PublicNameTemplate = template.PublicNameTemplate,
            DescriptionTemplate = template.DescriptionTemplate,
            Variables = template.Variables,
            ChecklistItemTemplates = template.ChecklistItemTemplates,
            ChapterId = template.ChapterId,
            CreatedAt = template.CreatedAt
        }, cancellation: ct);
    }
}
