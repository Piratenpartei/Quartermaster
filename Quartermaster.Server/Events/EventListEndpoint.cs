using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Events;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.Events;

public class EventListEndpoint : Endpoint<EventSearchRequest, EventSearchResponse> {
    private readonly EventRepository _eventRepo;
    private readonly ChapterRepository _chapterRepo;

    public EventListEndpoint(EventRepository eventRepo, ChapterRepository chapterRepo) {
        _eventRepo = eventRepo;
        _chapterRepo = chapterRepo;
    }

    public override void Configure() {
        Get("/api/events");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EventSearchRequest req, CancellationToken ct) {
        var (items, totalCount) = _eventRepo.Search(req.ChapterId, req.IncludeArchived, req.Page, req.PageSize);
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
                IsArchived = e.IsArchived,
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
}
