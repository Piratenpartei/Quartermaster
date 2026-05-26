using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Meetings;
using Quartermaster.Data.Meetings;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Meetings;

public class AgendaItemCompleteRequest {
    public Guid MeetingId { get; set; }
    public Guid ItemId { get; set; }
}

public class AgendaItemCompleteEndpoint : Endpoint<AgendaItemCompleteRequest> {
    private readonly MeetingRepository _meetingRepo;
    private readonly AgendaItemRepository _agendaRepo;
    private readonly IMeetingNotifier _notifier;
    private readonly PermissionContext _perms;

    public AgendaItemCompleteEndpoint(
        MeetingRepository meetingRepo,
        AgendaItemRepository agendaRepo,
        IMeetingNotifier notifier,
        PermissionContext perms) {
        _meetingRepo = meetingRepo;
        _agendaRepo = agendaRepo;
        _notifier = notifier;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/meetings/{MeetingId}/agenda/{ItemId}/complete");
    }

    public override async Task HandleAsync(AgendaItemCompleteRequest req, CancellationToken ct) {
        var meeting = _meetingRepo.Get(req.MeetingId);
        if (meeting == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        var item = _agendaRepo.Get(req.ItemId);
        if (item == null || item.MeetingId != req.MeetingId) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.Has(meeting.ChapterId, PermissionIdentifier.EditMeetings)) {
            await SendForbiddenAsync(ct);
            return;
        }

        if (meeting.Status != MeetingStatus.InProgress) {
            ThrowError(I18nKey.Error.Meeting.Agenda.CompleteRequiresInProgress);
            return;
        }

        _agendaRepo.MarkCompleted(req.ItemId);
        await _notifier.NotifyAgendaItemChangedAsync(req.MeetingId, req.ItemId, "completed");
        await SendOkAsync(ct);
    }
}
