using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Events;

public class EventCreateEndpoint : Endpoint<EventCreateRequest, EventDetailDTO> {
    private readonly EventRepository _eventRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly PermissionContext _perms;

    public EventCreateEndpoint(EventRepository eventRepo, ChapterRepository chapterRepo, PermissionContext perms) {
        _eventRepo = eventRepo;
        _chapterRepo = chapterRepo;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/events");
    }

    public override async Task HandleAsync(EventCreateRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.Has(req.ChapterId, PermissionIdentifier.CreateEvents)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var ev = Event.FromCreateRequest(req, DateTime.UtcNow);
        _eventRepo.Create(ev);

        var chapter = _chapterRepo.Get(ev.ChapterId);
        await SendAsync(ev.ToDetailDto(chapter?.Name ?? ""), cancellation: ct);
    }
}
