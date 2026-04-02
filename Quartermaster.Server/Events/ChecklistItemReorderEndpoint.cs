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

public class ChecklistItemReorderRequest {
    public Guid EventId { get; set; }
    public Guid ItemId { get; set; }
    public int Direction { get; set; }
}

public class ChecklistItemReorderEndpoint : Endpoint<ChecklistItemReorderRequest> {
    private readonly EventRepository _eventRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly ChapterRepository _chapterRepo;

    public ChecklistItemReorderEndpoint(EventRepository eventRepo,
        UserChapterPermissionRepository chapterPermRepo, UserGlobalPermissionRepository globalPermRepo,
        ChapterRepository chapterRepo) {
        _eventRepo = eventRepo;
        _chapterPermRepo = chapterPermRepo;
        _globalPermRepo = globalPermRepo;
        _chapterRepo = chapterRepo;
    }

    public override void Configure() {
        Post("/api/events/{EventId}/checklist/{ItemId}/reorder");
    }

    public override async Task HandleAsync(ChecklistItemReorderRequest req, CancellationToken ct) {
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
        if (!EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, PermissionIdentifier.EditEvents, _globalPermRepo) &&
            !_chapterPermRepo.HasPermissionWithInheritance(userId.Value, ev.ChapterId, PermissionIdentifier.EditEvents, _chapterRepo)) {
            await SendForbiddenAsync(ct);
            return;
        }

        _eventRepo.SwapChecklistItemOrder(req.EventId, req.ItemId, req.Direction);
        await SendOkAsync(ct);
    }
}
