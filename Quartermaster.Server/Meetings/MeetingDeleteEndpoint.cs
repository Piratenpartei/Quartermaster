using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Data.Meetings;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Meetings;

public class MeetingDeleteRequest {
    public Guid Id { get; set; }
}

public class MeetingDeleteEndpoint : Endpoint<MeetingDeleteRequest> {
    private readonly MeetingRepository _meetingRepo;
    private readonly PermissionContext _perms;

    public MeetingDeleteEndpoint(
        MeetingRepository meetingRepo,
        PermissionContext perms) {
        _meetingRepo = meetingRepo;
        _perms = perms;
    }

    public override void Configure() {
        Delete("/api/meetings/{Id}");
    }

    public override async Task HandleAsync(MeetingDeleteRequest req, CancellationToken ct) {
        var meeting = _meetingRepo.Get(req.Id);
        if (meeting == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.Has(meeting.ChapterId, PermissionIdentifier.DeleteMeetings)) {
            await SendForbiddenAsync(ct);
            return;
        }

        _meetingRepo.SoftDelete(req.Id);
        await SendOkAsync(ct);
    }
}
