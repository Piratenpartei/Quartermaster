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

public class AgendaItemDeleteRequest {
    public Guid MeetingId { get; set; }
    public Guid ItemId { get; set; }
}

public class AgendaItemDeleteEndpoint : Endpoint<AgendaItemDeleteRequest> {
    private readonly MeetingRepository _meetingRepo;
    private readonly AgendaItemRepository _agendaRepo;
    private readonly PermissionContext _perms;

    public AgendaItemDeleteEndpoint(
        MeetingRepository meetingRepo,
        AgendaItemRepository agendaRepo,
        PermissionContext perms) {
        _meetingRepo = meetingRepo;
        _agendaRepo = agendaRepo;
        _perms = perms;
    }

    public override void Configure() {
        Delete("/api/meetings/{MeetingId}/agenda/{ItemId}");
    }

    public override async Task HandleAsync(AgendaItemDeleteRequest req, CancellationToken ct) {
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

        if (meeting.Status != MeetingStatus.Draft && meeting.Status != MeetingStatus.Scheduled) {
            ThrowError(I18nKey.Error.Meeting.Agenda.DeleteStatusInvalid);
            return;
        }

        _agendaRepo.Delete(req.ItemId);
        await SendOkAsync(ct);
    }
}
