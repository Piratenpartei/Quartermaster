using System;
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

public class EventTemplateDetailRequest {
    public Guid Id { get; set; }
}

public class EventTemplateDetailEndpoint : Endpoint<EventTemplateDetailRequest, EventTemplateDetailDTO> {
    private readonly EventRepository _eventRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly ChapterRepository _chapterRepo;

    public EventTemplateDetailEndpoint(EventRepository eventRepo,
        UserChapterPermissionRepository chapterPermRepo, UserGlobalPermissionRepository globalPermRepo,
        ChapterRepository chapterRepo) {
        _eventRepo = eventRepo;
        _chapterPermRepo = chapterPermRepo;
        _globalPermRepo = globalPermRepo;
        _chapterRepo = chapterRepo;
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

        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        if (template.ChapterId.HasValue) {
            if (!EndpointAuthorizationHelper.HasPermission(userId.Value, template.ChapterId.Value, PermissionIdentifier.ViewTemplates, _globalPermRepo, _chapterPermRepo, _chapterRepo)) {
                await SendForbiddenAsync(ct);
                return;
            }
        } else {
            if (!EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, PermissionIdentifier.ViewTemplates, _globalPermRepo)) {
                await SendForbiddenAsync(ct);
                return;
            }
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
