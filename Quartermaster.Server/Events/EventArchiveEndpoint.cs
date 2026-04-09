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

public class EventArchiveRequest {
    public Guid Id { get; set; }
}

public class EventArchiveEndpoint : Endpoint<EventArchiveRequest> {
    private readonly EventRepository _eventRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly ChapterRepository _chapterRepo;

    public EventArchiveEndpoint(EventRepository eventRepo,
        UserChapterPermissionRepository chapterPermRepo, UserGlobalPermissionRepository globalPermRepo,
        ChapterRepository chapterRepo) {
        _eventRepo = eventRepo;
        _chapterPermRepo = chapterPermRepo;
        _globalPermRepo = globalPermRepo;
        _chapterRepo = chapterRepo;
    }

    public override void Configure() {
        Post("/api/events/{Id}/archive");
    }

    public override async Task HandleAsync(EventArchiveRequest req, CancellationToken ct) {
        var ev = _eventRepo.Get(req.Id);
        if (ev == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!EndpointAuthorizationHelper.HasPermission(userId.Value, ev.ChapterId, PermissionIdentifier.DeleteEvents, _globalPermRepo, _chapterPermRepo, _chapterRepo)) {
            await SendForbiddenAsync(ct);
            return;
        }

        // Toggle between Archived and Completed (the "un-archive" target)
        var newStatus = ev.Status == EventStatus.Archived ? EventStatus.Completed : EventStatus.Archived;
        _eventRepo.SetStatus(ev.Id, newStatus);
        await SendOkAsync(ct);
    }
}
