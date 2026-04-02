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

public class ChecklistItemCheckEndpoint : Endpoint<ChecklistItemCheckRequest> {
    private readonly EventRepository _eventRepo;
    private readonly ChecklistItemExecutor _executor;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly ChapterRepository _chapterRepo;

    public ChecklistItemCheckEndpoint(EventRepository eventRepo, ChecklistItemExecutor executor,
        UserChapterPermissionRepository chapterPermRepo, UserGlobalPermissionRepository globalPermRepo,
        ChapterRepository chapterRepo) {
        _eventRepo = eventRepo;
        _executor = executor;
        _chapterPermRepo = chapterPermRepo;
        _globalPermRepo = globalPermRepo;
        _chapterRepo = chapterRepo;
    }

    public override void Configure() {
        Post("/api/events/{EventId}/checklist/{ItemId}/check");
    }

    public override async Task HandleAsync(ChecklistItemCheckRequest req, CancellationToken ct) {
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

        var item = _eventRepo.GetChecklistItem(req.ItemId);
        if (item == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (item.IsCompleted) {
            ThrowError("Item is already completed.");
            return;
        }

        Guid? resultId = null;

        if (req.ExecuteAction && item.ItemType != ChecklistItemType.Text) {
            var parentEvent = _eventRepo.Get(item.EventId);
            var (execResultId, error) = _executor.Execute(item, parentEvent);
            if (error != null) {
                ThrowError(error);
                return;
            }
            resultId = execResultId;
        }

        _eventRepo.CheckItem(req.ItemId, resultId);

        await SendOkAsync(ct);
    }
}
