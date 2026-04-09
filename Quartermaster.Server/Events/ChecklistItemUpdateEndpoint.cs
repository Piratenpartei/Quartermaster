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

public class ChecklistItemUpdateEndpoint : Endpoint<ChecklistItemUpdateRequest> {
    private readonly EventRepository _eventRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly ChapterRepository _chapterRepo;

    public ChecklistItemUpdateEndpoint(EventRepository eventRepo,
        UserChapterPermissionRepository chapterPermRepo, UserGlobalPermissionRepository globalPermRepo,
        ChapterRepository chapterRepo) {
        _eventRepo = eventRepo;
        _chapterPermRepo = chapterPermRepo;
        _globalPermRepo = globalPermRepo;
        _chapterRepo = chapterRepo;
    }

    public override void Configure() {
        Put("/api/events/{EventId}/checklist/{ItemId}");
    }

    public override async Task HandleAsync(ChecklistItemUpdateRequest req, CancellationToken ct) {
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
        if (!EndpointAuthorizationHelper.HasPermission(userId.Value, ev.ChapterId, PermissionIdentifier.EditEvents, _globalPermRepo, _chapterPermRepo, _chapterRepo)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var item = new EventChecklistItem {
            Id = req.ItemId,
            EventId = req.EventId,
            SortOrder = req.SortOrder,
            ItemType = (ChecklistItemType)req.ItemType,
            Label = req.Label,
            Configuration = req.Configuration
        };

        _eventRepo.UpdateChecklistItem(item);

        await SendOkAsync(ct);
    }
}
