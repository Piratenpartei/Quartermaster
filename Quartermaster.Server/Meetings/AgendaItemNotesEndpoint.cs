using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Meetings;
using Quartermaster.Data.Meetings;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Meetings;

public class AgendaItemNotesEndpoint : Endpoint<AgendaItemNotesRequest> {
    private readonly MeetingRepository _meetingRepo;
    private readonly AgendaItemRepository _agendaRepo;
    private readonly PermissionContext _perms;

    public AgendaItemNotesEndpoint(
        MeetingRepository meetingRepo,
        AgendaItemRepository agendaRepo,
        PermissionContext perms) {
        _meetingRepo = meetingRepo;
        _agendaRepo = agendaRepo;
        _perms = perms;
    }

    public override void Configure() {
        Put("/api/meetings/{MeetingId}/agenda/{ItemId}/notes");
    }

    public override async Task HandleAsync(AgendaItemNotesRequest req, CancellationToken ct) {
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

        _agendaRepo.UpdateNotes(req.ItemId, req.Notes);
        await SendOkAsync(ct);
    }
}
