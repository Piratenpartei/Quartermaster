using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Events;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.Events;

public class EventDetailRequest {
    public Guid Id { get; set; }
}

public class EventDetailEndpoint : Endpoint<EventDetailRequest, EventDetailDTO> {
    private readonly EventRepository _eventRepo;
    private readonly ChapterRepository _chapterRepo;

    public EventDetailEndpoint(EventRepository eventRepo, ChapterRepository chapterRepo) {
        _eventRepo = eventRepo;
        _chapterRepo = chapterRepo;
    }

    public override void Configure() {
        Get("/api/events/{Id}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EventDetailRequest req, CancellationToken ct) {
        var ev = _eventRepo.Get(req.Id);
        if (ev == null) {
            await SendNotFoundAsync(ct);
            return;
        }

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
            IsArchived = ev.IsArchived,
            EventTemplateId = ev.EventTemplateId,
            CreatedAt = ev.CreatedAt,
            ChecklistItems = itemDtos
        }, cancellation: ct);
    }
}
