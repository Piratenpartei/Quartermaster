using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Events;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.Events;

public class EventTemplateCreateEndpoint : Endpoint<EventTemplateCreateRequest, EventTemplateDetailDTO> {
    private readonly EventRepository _eventRepo;

    public EventTemplateCreateEndpoint(EventRepository eventRepo) {
        _eventRepo = eventRepo;
    }

    public override void Configure() {
        Post("/api/eventtemplates");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EventTemplateCreateRequest req, CancellationToken ct) {
        var ev = _eventRepo.Get(req.EventId);
        if (ev == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var checklistItems = _eventRepo.GetChecklistItems(req.EventId);

        var checklistTemplates = checklistItems
            .OrderBy(i => i.SortOrder)
            .Select(i => new {
                sortOrder = i.SortOrder,
                itemType = (int)i.ItemType,
                label = i.Label,
                configuration = i.Configuration
            })
            .ToList();

        var checklistJson = JsonSerializer.Serialize(checklistTemplates);

        var template = new EventTemplate {
            Name = req.Name,
            PublicNameTemplate = ev.PublicName,
            DescriptionTemplate = ev.Description,
            Variables = req.Variables,
            ChecklistItemTemplates = checklistJson,
            ChapterId = ev.ChapterId,
            CreatedAt = DateTime.UtcNow
        };

        _eventRepo.CreateTemplate(template);

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
