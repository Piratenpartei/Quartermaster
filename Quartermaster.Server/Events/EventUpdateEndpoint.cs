using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.Events.Subscriptions;

namespace Quartermaster.Server.Events;

public class EventUpdateEndpoint : Endpoint<EventUpdateRequest> {
    private readonly EventRepository _eventRepo;
    private readonly EventScheduleChangeNotice _changeNotice;
    private readonly PermissionContext _perms;

    public EventUpdateEndpoint(EventRepository eventRepo, EventScheduleChangeNotice changeNotice, PermissionContext perms) {
        _eventRepo = eventRepo;
        _changeNotice = changeNotice;
        _perms = perms;
    }

    public override void Configure() {
        Put("/api/events/{Id}");
    }

    public override async Task HandleAsync(EventUpdateRequest req, CancellationToken ct) {
        var existing = _eventRepo.Get(req.Id);
        if (existing == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasOnChapterOrAncestors(existing.ChapterId, PermissionIdentifier.EditEvents)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var ev = new Event {
            Id = req.Id,
            InternalName = req.InternalName,
            PublicName = req.PublicName,
            Description = req.Description,
            EventDate = req.EventDate.ToStorage(),
            StartTime = req.StartTime.ToStorage(),
            MeetingUrl = string.IsNullOrWhiteSpace(req.MeetingUrl) ? null : req.MeetingUrl.Trim(),
            Visibility = req.Visibility
        };

        _eventRepo.Update(ev);

        var updated = _eventRepo.Get(req.Id);
        if (updated != null)
            _changeNotice.NotifyIfChanged(existing, updated, DateTime.UtcNow);

        await SendOkAsync(ct);
    }
}
