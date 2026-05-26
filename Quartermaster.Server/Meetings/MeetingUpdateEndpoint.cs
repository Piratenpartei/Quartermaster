using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Meetings;
using Quartermaster.Data.Meetings;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Meetings;

public class MeetingUpdateEndpoint : Endpoint<MeetingUpdateRequest> {
    private readonly MeetingRepository _meetingRepo;
    private readonly PermissionContext _perms;

    public MeetingUpdateEndpoint(
        MeetingRepository meetingRepo,
        PermissionContext perms) {
        _meetingRepo = meetingRepo;
        _perms = perms;
    }

    public override void Configure() {
        Put("/api/meetings/{Id}");
    }

    public override async Task HandleAsync(MeetingUpdateRequest req, CancellationToken ct) {
        var existing = _meetingRepo.Get(req.Id);
        if (existing == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.Has(existing.ChapterId, PermissionIdentifier.EditMeetings)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var updated = new Meeting {
            Id = req.Id,
            Title = req.Title,
            Visibility = req.Visibility,
            MeetingDate = req.MeetingDate,
            Location = req.Location,
            Description = req.Description
        };

        _meetingRepo.Update(updated);
        await SendOkAsync(ct);
    }
}
