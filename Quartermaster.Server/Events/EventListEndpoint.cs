using System;
using System.Collections.Generic;
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

public class EventListEndpoint : Endpoint<EventSearchRequest, EventSearchResponse> {
    private readonly EventRepository _eventRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly PermissionContext _perms;

    public EventListEndpoint(EventRepository eventRepo, ChapterRepository chapterRepo, PermissionContext perms) {
        _eventRepo = eventRepo;
        _chapterRepo = chapterRepo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/events");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EventSearchRequest req, CancellationToken ct) {
        if (req.Managed && _perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var (items, totalCount) = req.Managed ? SearchManaged(req) : SearchPublic(req);
        var chapters = _chapterRepo.GetAll().ToDictionary(c => c.Id, c => c.Name);

        var eventIds = req.Managed ? items.Select(e => e.Id).ToList() : new List<Guid>();
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
                EventDate = e.EventDate.ToDtoDate(),
                StartTime = e.StartTime.ToDtoTime(),
                MeetingUrl = e.MeetingUrl,
                Status = e.Status,
                Visibility = e.Visibility,
                ChecklistTotal = counts.Total,
                ChecklistCompleted = counts.Completed,
                CreatedAt = e.CreatedAt.ToDtoUtc()
            };
        }).ToList();

        await SendAsync(new EventSearchResponse {
            Items = dtos,
            TotalCount = totalCount,
            Page = req.Page,
            PageSize = req.PageSize
        }, cancellation: ct);
    }

    private (List<Event> Items, int TotalCount) SearchManaged(EventSearchRequest req) {
        var chapterIds = ManagedChapterIds();
        if (chapterIds is { Count: 0 })
            return (new List<Event>(), 0);
        return _eventRepo.Search(req.ChapterId, req.IncludeArchived, req.Page, req.PageSize, chapterIds: chapterIds);
    }

    /// <summary>Private events only of the chapters whose events the caller may view, as on the event page.</summary>
    private (List<Event> Items, int TotalCount) SearchPublic(EventSearchRequest req) {
        var allowedVisibilities = GetAllowedVisibilities();
        var privateChapterIds = allowedVisibilities.Contains(EventVisibility.Private)
            ? _perms.GetPermittedChapterIds(PermissionIdentifier.ViewEvents)
            : null;
        return _eventRepo.Search(req.ChapterId, req.IncludeArchived, req.Page, req.PageSize, allowedVisibilities, privateChapterIds);
    }

    /// <summary>Chapters whose events the caller may view or edit, including those below where the grant passes down; null for all.</summary>
    private List<Guid>? ManagedChapterIds() {
        var viewable = _perms.GetPermittedChapterIds(PermissionIdentifier.ViewEvents);
        var editable = _perms.GetPermittedChapterIds(PermissionIdentifier.EditEvents);
        if (viewable == null || editable == null)
            return null;
        return viewable.Union(editable).ToList();
    }

    /// <summary>
    /// Returns the set of event visibilities the current requester is allowed to see.
    /// - Anonymous: Public only
    /// - Authenticated (any user): Public + MembersOnly
    /// - Users with ViewEvents (global or any chapter): all three (Private included)
    /// </summary>
    private List<EventVisibility> GetAllowedVisibilities() {
        if (_perms.UserId == null)
            return new List<EventVisibility> { EventVisibility.Public };

        var permittedChapterIds = _perms.GetPermittedChapterIds(PermissionIdentifier.ViewEvents);
        var hasViewAnywhere = permittedChapterIds == null || permittedChapterIds.Count > 0;

        if (hasViewAnywhere)
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
