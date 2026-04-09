using System;
using System.Linq;
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

public class EventDetailRequest {
    public Guid Id { get; set; }
}

public class EventDetailEndpoint : Endpoint<EventDetailRequest, EventDetailDTO> {
    private readonly EventRepository _eventRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;

    public EventDetailEndpoint(EventRepository eventRepo, ChapterRepository chapterRepo,
        UserChapterPermissionRepository chapterPermRepo, UserGlobalPermissionRepository globalPermRepo) {
        _eventRepo = eventRepo;
        _chapterRepo = chapterRepo;
        _chapterPermRepo = chapterPermRepo;
        _globalPermRepo = globalPermRepo;
    }

    public override void Configure() {
        Get("/api/events/{Id}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EventDetailRequest req, CancellationToken ct) {
        var ev = _eventRepo.RefreshStatus(req.Id);
        if (ev == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var userId = EndpointAuthorizationHelper.GetUserId(User);

        // Visibility gate: Public events are always visible, MembersOnly needs auth, Private needs ViewEvents
        if (ev.Visibility == EventVisibility.Private) {
            if (userId == null) {
                await SendUnauthorizedAsync(ct);
                return;
            }
            if (!EndpointAuthorizationHelper.HasPermission(userId.Value, ev.ChapterId, PermissionIdentifier.ViewEvents, _globalPermRepo, _chapterPermRepo, _chapterRepo)) {
                await SendForbiddenAsync(ct);
                return;
            }
        } else if (ev.Visibility == EventVisibility.MembersOnly) {
            if (userId == null) {
                await SendUnauthorizedAsync(ct);
                return;
            }
        }
        // Public: no check

        var chapter = _chapterRepo.Get(ev.ChapterId);
        var checklistItems = _eventRepo.GetChecklistItems(ev.Id);

        var itemDtos = checklistItems.Select(i => new EventChecklistItemDTO {
            Id = i.Id,
            SortOrder = i.SortOrder,
            ItemType = (int)i.ItemType,
            Label = i.Label,
            IsCompleted = i.IsCompleted,
            CompletedAt = i.CompletedAt,
            Configuration = i.Configuration,
            ResultId = i.ResultId
        }).ToList();

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
            ChecklistItems = itemDtos
        }, cancellation: ct);
    }
}
