using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Meetings;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Meetings;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Meetings;

public class MeetingCreateEndpoint : Endpoint<MeetingCreateRequest, MeetingDTO> {
    private readonly MeetingRepository _meetingRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly PermissionContext _perms;

    public MeetingCreateEndpoint(
        MeetingRepository meetingRepo,
        ChapterRepository chapterRepo,
        PermissionContext perms) {
        _meetingRepo = meetingRepo;
        _chapterRepo = chapterRepo;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/meetings");
    }

    public override async Task HandleAsync(MeetingCreateRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.Has(req.ChapterId, PermissionIdentifier.CreateMeetings)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var chapter = _chapterRepo.Get(req.ChapterId);
        if (chapter == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var meeting = new Meeting {
            ChapterId = req.ChapterId,
            Title = req.Title,
            Visibility = req.Visibility,
            MeetingDate = req.MeetingDate.ToStorage(),
            Location = req.Location,
            Description = req.Description,
            Status = MeetingStatus.Draft
        };

        _meetingRepo.Create(meeting);

        await SendAsync(new MeetingDTO {
            Id = meeting.Id,
            ChapterId = meeting.ChapterId,
            ChapterName = chapter.Name,
            Title = meeting.Title,
            MeetingDate = meeting.MeetingDate.ToDtoDate(),
            Status = meeting.Status,
            Visibility = meeting.Visibility,
            Location = meeting.Location,
            AgendaItemCount = 0
        }, cancellation: ct);
    }
}
