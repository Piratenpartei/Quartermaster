using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Api.I18n;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Events;

public class EventTemplateCreateEndpoint : Endpoint<EventTemplateCreateRequest, EventTemplateDetailDTO> {
    private readonly EventRepository _eventRepo;
    private readonly PermissionContext _perms;

    public EventTemplateCreateEndpoint(EventRepository eventRepo, PermissionContext perms) {
        _eventRepo = eventRepo;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/eventtemplates");
    }

    public override async Task HandleAsync(EventTemplateCreateRequest req, CancellationToken ct) {
        var ev = _eventRepo.Get(req.EventId);
        if (ev == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.Has(ev.ChapterId, PermissionIdentifier.EditTemplates)) {
            await SendForbiddenAsync(ct);
            return;
        }

        if (ev.Status != EventStatus.Draft) {
            ThrowError(I18nKey.Error.Event.Template.OnlyFromDraft);
            return;
        }

        var checklistItems = _eventRepo.GetChecklistItems(req.EventId);

        var checklistTemplates = checklistItems
            .OrderBy(i => i.SortOrder)
            .Select(i => new EventChecklistItemTemplateDTO {
                SortOrder = i.SortOrder,
                ItemType = i.ItemType,
                Label = i.Label,
                Configuration = EventConfigSerializer.ParseConfig(i.Configuration)
            })
            .ToList();

        var template = new EventTemplate {
            Name = req.Name,
            PublicNameTemplate = ev.PublicName,
            DescriptionTemplate = ev.Description,
            Variables = EventConfigSerializer.Serialize(req.Variables),
            ChecklistItemTemplates = EventConfigSerializer.Serialize(checklistTemplates),
            ChapterId = ev.ChapterId,
            CreatedAt = DateTime.UtcNow
        };

        _eventRepo.CreateTemplate(template);

        await SendAsync(new EventTemplateDetailDTO {
            Id = template.Id,
            Name = template.Name,
            PublicNameTemplate = template.PublicNameTemplate,
            DescriptionTemplate = template.DescriptionTemplate,
            Variables = req.Variables,
            ChecklistItemTemplates = checklistTemplates,
            ChapterId = template.ChapterId,
            CreatedAt = template.CreatedAt.ToDtoUtc()
        }, cancellation: ct);
    }
}
