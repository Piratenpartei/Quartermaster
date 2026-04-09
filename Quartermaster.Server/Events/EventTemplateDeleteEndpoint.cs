using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Events;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Events;

public class EventTemplateDeleteRequest {
    public Guid Id { get; set; }
}

public class EventTemplateDeleteEndpoint : Endpoint<EventTemplateDeleteRequest> {
    private readonly EventRepository _eventRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly ChapterRepository _chapterRepo;

    public EventTemplateDeleteEndpoint(EventRepository eventRepo,
        UserChapterPermissionRepository chapterPermRepo, UserGlobalPermissionRepository globalPermRepo,
        ChapterRepository chapterRepo) {
        _eventRepo = eventRepo;
        _chapterPermRepo = chapterPermRepo;
        _globalPermRepo = globalPermRepo;
        _chapterRepo = chapterRepo;
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

        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        if (template.ChapterId.HasValue) {
            if (!EndpointAuthorizationHelper.HasPermission(userId.Value, template.ChapterId.Value, PermissionIdentifier.EditTemplates, _globalPermRepo, _chapterPermRepo, _chapterRepo)) {
                await SendForbiddenAsync(ct);
                return;
            }
        } else {
            if (!EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, PermissionIdentifier.EditTemplates, _globalPermRepo)) {
                await SendForbiddenAsync(ct);
                return;
            }
        }

        _eventRepo.DeleteTemplate(req.Id);
        await SendOkAsync(ct);
    }
}
