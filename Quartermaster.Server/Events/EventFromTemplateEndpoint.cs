using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Events;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Events;

public class EventFromTemplateEndpoint : Endpoint<EventFromTemplateRequest, EventDetailDTO> {
    private readonly EventRepository _eventRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;

    public EventFromTemplateEndpoint(EventRepository eventRepo, ChapterRepository chapterRepo,
        UserChapterPermissionRepository chapterPermRepo, UserGlobalPermissionRepository globalPermRepo) {
        _eventRepo = eventRepo;
        _chapterRepo = chapterRepo;
        _chapterPermRepo = chapterPermRepo;
        _globalPermRepo = globalPermRepo;
    }

    public override void Configure() {
        Post("/api/events/from-template");
    }

    public override async Task HandleAsync(EventFromTemplateRequest req, CancellationToken ct) {
        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, PermissionIdentifier.CreateEvents, _globalPermRepo) &&
            !_chapterPermRepo.HasPermissionWithInheritance(userId.Value, req.ChapterId, PermissionIdentifier.CreateEvents, _chapterRepo)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var template = _eventRepo.GetTemplate(req.TemplateId);
        if (template == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        // Built-in date variables always come from EventDate (override any custom values)
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
            EventDate = req.EventDate,
            EventTemplateId = template.Id,
            CreatedAt = DateTime.UtcNow
        };

        _eventRepo.Create(ev);

        var checklistItemDtos = new List<EventChecklistItemDTO>();

        var itemTemplates = JsonSerializer.Deserialize<List<ChecklistItemTemplate>>(
            template.ChecklistItemTemplates) ?? new List<ChecklistItemTemplate>();

        foreach (var itemTemplate in itemTemplates) {
            var label = ReplaceVariables(itemTemplate.Label, req.VariableValues);
            var configuration = itemTemplate.Configuration != null
                ? ReplaceVariables(itemTemplate.Configuration, req.VariableValues)
                : null;

            var checklistItem = new EventChecklistItem {
                EventId = ev.Id,
                SortOrder = itemTemplate.SortOrder,
                ItemType = (ChecklistItemType)itemTemplate.ItemType,
                Label = label,
                Configuration = configuration
            };

            _eventRepo.CreateChecklistItem(checklistItem);

            checklistItemDtos.Add(new EventChecklistItemDTO {
                Id = checklistItem.Id,
                SortOrder = checklistItem.SortOrder,
                ItemType = (int)checklistItem.ItemType,
                Label = checklistItem.Label,
                IsCompleted = false,
                Configuration = checklistItem.Configuration
            });
        }

        var chapter = _chapterRepo.Get(req.ChapterId);

        await SendAsync(new EventDetailDTO {
            Id = ev.Id,
            ChapterId = ev.ChapterId,
            ChapterName = chapter?.Name ?? "",
            InternalName = ev.InternalName,
            PublicName = ev.PublicName,
            Description = ev.Description,
            EventDate = ev.EventDate,
            Status = ev.Status,
            Visibility = ev.Visibility,
            EventTemplateId = ev.EventTemplateId,
            CreatedAt = ev.CreatedAt,
            ChecklistItems = checklistItemDtos
        }, cancellation: ct);
    }

    private static string ReplaceVariables(string text, Dictionary<string, string> values) {
        foreach (var (name, value) in values)
            text = text.Replace($"{{{{{name}}}}}", value);

        return text;
    }

    private class ChecklistItemTemplate {
        [System.Text.Json.Serialization.JsonPropertyName("sortOrder")]
        public int SortOrder { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("itemType")]
        public int ItemType { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("label")]
        public string Label { get; set; } = "";
        [System.Text.Json.Serialization.JsonPropertyName("configuration")]
        public string? Configuration { get; set; }
    }
}
