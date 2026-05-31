using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Events;

public class EventFromTemplateEndpoint : Endpoint<EventFromTemplateRequest, EventDetailDTO> {
    private readonly EventRepository _eventRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly PermissionContext _perms;

    public EventFromTemplateEndpoint(EventRepository eventRepo, ChapterRepository chapterRepo, PermissionContext perms) {
        _eventRepo = eventRepo;
        _chapterRepo = chapterRepo;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/events/from-template");
    }

    public override async Task HandleAsync(EventFromTemplateRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.Has(req.ChapterId, PermissionIdentifier.CreateEvents)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var template = _eventRepo.GetTemplate(req.TemplateId);
        if (template == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var dateStr = req.EventDate?.ToString("dd.MM.yyyy") ?? "";
        req.VariableValues["date"] = dateStr;
        req.VariableValues["datum"] = dateStr;

        var publicName = ReplaceVariables(template.PublicNameTemplate, req.VariableValues);
        var description = template.DescriptionTemplate != null
            ? ReplaceVariables(template.DescriptionTemplate, req.VariableValues)
            : null;

        var ev = new Event {
            ChapterId = req.ChapterId,
            InternalName = publicName,
            PublicName = publicName,
            Description = description,
            EventDate = req.EventDate.ToStorage(),
            EventTemplateId = template.Id,
            CreatedAt = DateTime.UtcNow
        };
        _eventRepo.Create(ev);

        var checklistItemDtos = new List<EventChecklistItemDTO>();
        var itemTemplates = EventConfigSerializer.ParseTemplates(template.ChecklistItemTemplates);

        foreach (var itemTemplate in itemTemplates) {
            var label = ReplaceVariables(itemTemplate.Label, req.VariableValues);
            var configuration = EventConfigSerializer.ApplyVariables(itemTemplate.Configuration, req.VariableValues);

            var checklistItem = new EventChecklistItem {
                EventId = ev.Id,
                SortOrder = itemTemplate.SortOrder,
                ItemType = itemTemplate.ItemType,
                Label = label,
                Configuration = configuration != null ? EventConfigSerializer.Serialize(configuration) : null
            };
            _eventRepo.CreateChecklistItem(checklistItem);
            checklistItemDtos.Add(checklistItem.ToDto(configuration));
        }

        var chapter = _chapterRepo.Get(req.ChapterId);
        var dto = ev.ToDetailDto(chapter?.Name ?? "");
        dto.ChecklistItems = checklistItemDtos;
        await SendAsync(dto, cancellation: ct);
    }

    private static string ReplaceVariables(string text, Dictionary<string, string> values) {
        foreach (var (name, value) in values)
            text = text.Replace($"{{{{{name}}}}}", value);
        return text;
    }
}
