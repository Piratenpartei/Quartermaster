using System.Collections.Generic;
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

public class EventListEndpoint : Endpoint<EventSearchRequest, EventSearchResponse> {
    private readonly EventRepository _eventRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;

    public EventListEndpoint(EventRepository eventRepo, ChapterRepository chapterRepo,
        UserGlobalPermissionRepository globalPermRepo, UserChapterPermissionRepository chapterPermRepo) {
        _eventRepo = eventRepo;
        _chapterRepo = chapterRepo;
        _globalPermRepo = globalPermRepo;
        _chapterPermRepo = chapterPermRepo;
    }

    public override void Configure() {
        Get("/api/events");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EventSearchRequest req, CancellationToken ct) {
        var allowedVisibilities = GetAllowedVisibilities();
        var (items, totalCount) = _eventRepo.Search(req.ChapterId, req.IncludeArchived, req.Page, req.PageSize, allowedVisibilities);
        var chapters = _chapterRepo.GetAll().ToDictionary(c => c.Id, c => c.Name);

        var eventIds = items.Select(e => e.Id).ToList();
        var checklistCounts = eventIds.ToDictionary(
            id => id,
            id => {
                var checklistItems = _eventRepo.GetChecklistItems(id);
                return (Total: checklistItems.Count, Completed: checklistItems.Count(i => i.IsCompleted));
            });

        var dtos = items.Select(e => {
            var counts = checklistCounts.TryGetValue(e.Id, out var c) ? c : (Total: 0, Completed: 0);
            return new EventDTO {
                Id = e.Id,
                ChapterId = e.ChapterId,
                ChapterName = chapters.TryGetValue(e.ChapterId, out var name) ? name : "",
                PublicName = e.PublicName,
                EventDate = e.EventDate,
                Status = e.Status,
                Visibility = e.Visibility,
                ChecklistTotal = counts.Total,
                ChecklistCompleted = counts.Completed,
                CreatedAt = e.CreatedAt
            };
        }).ToList();

        await SendAsync(new EventSearchResponse {
            Items = dtos,
            TotalCount = totalCount
        }, cancellation: ct);
    }

    /// <summary>
    /// Returns the set of event visibilities the current requester is allowed to see.
    /// - Anonymous: Public only
    /// - Authenticated (any user): Public + MembersOnly
    /// - Users with ViewEvents (global or any chapter): all three (Private included)
    /// </summary>
    private List<EventVisibility> GetAllowedVisibilities() {
        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null)
            return new List<EventVisibility> { EventVisibility.Public };

        var hasViewGlobal = EndpointAuthorizationHelper.HasGlobalPermission(
            userId.Value, PermissionIdentifier.ViewEvents, _globalPermRepo);
        var hasViewInAnyChapter = _chapterPermRepo.GetAllForUser(userId.Value)
            .Any(kvp => kvp.Value.Contains(PermissionIdentifier.ViewEvents));

        if (hasViewGlobal || hasViewInAnyChapter)
            return new List<EventVisibility> {
                EventVisibility.Public,
                EventVisibility.MembersOnly,
                EventVisibility.Private
            };

        return new List<EventVisibility> {
            EventVisibility.Public,
            EventVisibility.MembersOnly
        };
    }
}
