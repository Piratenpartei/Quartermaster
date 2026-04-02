using System;
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

public class EventTemplateCreateEndpoint : Endpoint<EventTemplateCreateRequest, EventTemplateDetailDTO> {
    private readonly EventRepository _eventRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly ChapterRepository _chapterRepo;

    public EventTemplateCreateEndpoint(EventRepository eventRepo,
        UserChapterPermissionRepository chapterPermRepo, UserGlobalPermissionRepository globalPermRepo,
        ChapterRepository chapterRepo) {
        _eventRepo = eventRepo;
        _chapterPermRepo = chapterPermRepo;
        _globalPermRepo = globalPermRepo;
        _chapterRepo = chapterRepo;
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

        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, PermissionIdentifier.EditTemplates, _globalPermRepo) &&
            !_chapterPermRepo.HasPermissionWithInheritance(userId.Value, ev.ChapterId, PermissionIdentifier.EditTemplates, _chapterRepo)) {
            await SendForbiddenAsync(ct);
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
