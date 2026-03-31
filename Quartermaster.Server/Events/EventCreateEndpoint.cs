using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Events;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.Events;

public class EventCreateEndpoint : Endpoint<EventCreateRequest, EventDetailDTO> {
    private readonly EventRepository _eventRepo;
    private readonly ChapterRepository _chapterRepo;

    public EventCreateEndpoint(EventRepository eventRepo, ChapterRepository chapterRepo) {
        _eventRepo = eventRepo;
        _chapterRepo = chapterRepo;
    }

    public override void Configure() {
        Post("/api/events");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EventCreateRequest req, CancellationToken ct) {
        var ev = new Event {
            ChapterId = req.ChapterId,
            InternalName = req.InternalName,
            PublicName = req.PublicName,
            Description = req.Description,
            EventDate = req.EventDate,
            CreatedAt = DateTime.UtcNow
        };

        _eventRepo.Create(ev);

        var chapter = _chapterRepo.Get(ev.ChapterId);

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
            CreatedAt = ev.CreatedAt
        }, cancellation: ct);
    }
}
