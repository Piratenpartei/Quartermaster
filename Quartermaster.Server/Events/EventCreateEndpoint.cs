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

public class EventCreateEndpoint : Endpoint<EventCreateRequest, EventDetailDTO> {
    private readonly EventRepository _eventRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;

    public EventCreateEndpoint(EventRepository eventRepo, ChapterRepository chapterRepo,
        UserChapterPermissionRepository chapterPermRepo, UserGlobalPermissionRepository globalPermRepo) {
        _eventRepo = eventRepo;
        _chapterRepo = chapterRepo;
        _chapterPermRepo = chapterPermRepo;
        _globalPermRepo = globalPermRepo;
    }

    public override void Configure() {
        Post("/api/events");
    }

    public override async Task HandleAsync(EventCreateRequest req, CancellationToken ct) {
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

        var ev = new Event {
            ChapterId = req.ChapterId,
            InternalName = req.InternalName,
            PublicName = req.PublicName,
            Description = req.Description,
            EventDate = req.EventDate,
            Visibility = req.Visibility,
            CreatedAt = DateTime.UtcNow
        };

        _eventRepo.Create(ev);

        var chapter = _chapterRepo.Get(ev.ChapterId);

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
            CreatedAt = ev.CreatedAt
        }, cancellation: ct);
    }
}
