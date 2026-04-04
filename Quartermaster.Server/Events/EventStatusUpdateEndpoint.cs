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

public class EventStatusUpdateRequest {
    public Guid Id { get; set; }
    public EventStatus Status { get; set; }
}

public class EventStatusUpdateEndpoint : Endpoint<EventStatusUpdateRequest> {
    private readonly EventRepository _eventRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly ChapterRepository _chapterRepo;

    public EventStatusUpdateEndpoint(EventRepository eventRepo,
        UserChapterPermissionRepository chapterPermRepo, UserGlobalPermissionRepository globalPermRepo,
        ChapterRepository chapterRepo) {
        _eventRepo = eventRepo;
        _chapterPermRepo = chapterPermRepo;
        _globalPermRepo = globalPermRepo;
        _chapterRepo = chapterRepo;
    }

    public override void Configure() {
        Put("/api/events/{Id}/status");
    }

    public override async Task HandleAsync(EventStatusUpdateRequest req, CancellationToken ct) {
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

        // Archive/unarchive uses DeleteEvents; other transitions use EditEvents
        var requiredPerm = req.Status == EventStatus.Archived || ev.Status == EventStatus.Archived
            ? PermissionIdentifier.DeleteEvents
            : PermissionIdentifier.EditEvents;

        if (!EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, requiredPerm, _globalPermRepo) &&
            !_chapterPermRepo.HasPermissionWithInheritance(userId.Value, ev.ChapterId, requiredPerm, _chapterRepo)) {
            await SendForbiddenAsync(ct);
            return;
        }

        if (!IsTransitionAllowed(ev.Status, req.Status)) {
            ThrowError($"Übergang von {ev.Status} zu {req.Status} ist nicht erlaubt.");
            return;
        }

        _eventRepo.SetStatus(req.Id, req.Status);
        await SendOkAsync(ct);
    }

    private static bool IsTransitionAllowed(EventStatus from, EventStatus to) {
        if (from == to)
            return false;

        // Allowed manual transitions
        return (from, to) switch {
            (EventStatus.Draft, EventStatus.Active) => true,
            (EventStatus.Active, EventStatus.Draft) => true,
            (EventStatus.Active, EventStatus.Completed) => true,
            (EventStatus.Completed, EventStatus.Active) => true,
            (EventStatus.Completed, EventStatus.Archived) => true,
            (EventStatus.Archived, EventStatus.Completed) => true,
            _ => false
        };
    }
}
