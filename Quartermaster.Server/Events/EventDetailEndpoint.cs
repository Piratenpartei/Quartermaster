using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Events;

public class EventDetailRequest {
    public Guid Id { get; set; }
}

public class EventDetailEndpoint : Endpoint<EventDetailRequest, EventDetailDTO> {
    private readonly EventRepository _eventRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly PermissionContext _perms;
    private readonly EventTemplateAccess _templateAccess;

    public EventDetailEndpoint(EventRepository eventRepo, ChapterRepository chapterRepo, PermissionContext perms, EventTemplateAccess templateAccess) {
        _eventRepo = eventRepo;
        _chapterRepo = chapterRepo;
        _perms = perms;
        _templateAccess = templateAccess;
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

        if (ev.Visibility == EventVisibility.Private) {
            if (_perms.UserId == null) {
                await SendUnauthorizedAsync(ct);
                return;
            }
            if (!_perms.Has(ev.ChapterId, PermissionIdentifier.ViewEvents)) {
                await SendForbiddenAsync(ct);
                return;
            }
        } else if (ev.Visibility == EventVisibility.MembersOnly) {
            if (_perms.UserId == null) {
                await SendUnauthorizedAsync(ct);
                return;
            }
        }

        var chapter = _chapterRepo.Get(ev.ChapterId);
        var dto = ev.ToDetailDto(chapter?.Name ?? "");
        dto.CanManage = CanManage(ev);
        if (dto.CanManage) {
            dto.ChecklistItems = _eventRepo.GetChecklistItems(ev.Id)
                .Select(i => i.ToDto(EventConfigSerializer.ParseConfig(i.Configuration)))
                .ToList();
        } else {
            dto.InternalName = "";
        }
        if (_perms.UserId != null) {
            dto.CanEdit = _perms.HasOnChapterOrAncestors(ev.ChapterId, PermissionIdentifier.EditEvents);
            dto.CanArchive = _perms.HasOnChapterOrAncestors(ev.ChapterId, PermissionIdentifier.DeleteEvents);
            dto.CanSaveAsTemplate = _templateAccess.CanEditTemplatesOf(ev.ChapterId);
        }
        await SendAsync(dto, cancellation: ct);
    }

    /// <summary>The internal name and the checklist are for those who may view or edit the chapter's events, directly or from a chapter above.</summary>
    private bool CanManage(Event ev) {
        return _perms.Has(ev.ChapterId, PermissionIdentifier.ViewEvents)
            || _perms.HasOnChapterOrAncestors(ev.ChapterId, PermissionIdentifier.EditEvents);
    }
}
