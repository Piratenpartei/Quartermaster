using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Meetings;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Meetings;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Meetings;

public class MeetingCreateEndpoint : Endpoint<MeetingCreateRequest, MeetingDTO> {
    private readonly MeetingRepository _meetingRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;

    public MeetingCreateEndpoint(
        MeetingRepository meetingRepo,
        ChapterRepository chapterRepo,
        UserGlobalPermissionRepository globalPermRepo,
        UserChapterPermissionRepository chapterPermRepo) {
        _meetingRepo = meetingRepo;
        _chapterRepo = chapterRepo;
        _globalPermRepo = globalPermRepo;
        _chapterPermRepo = chapterPermRepo;
    }

    public override void Configure() {
        Post("/api/meetings");
    }

    public override async Task HandleAsync(MeetingCreateRequest req, CancellationToken ct) {
        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!EndpointAuthorizationHelper.HasPermission(userId.Value, req.ChapterId, PermissionIdentifier.CreateMeetings, _globalPermRepo, _chapterPermRepo, _chapterRepo)) {
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
            MeetingDate = req.MeetingDate,
            Location = req.Location,
            Description = req.Description,
            Status = MeetingStatus.Draft
        };

        _meetingRepo.Create(meeting);

        await SendAsync(MeetingDtoBuilder.BuildMeetingDTO(meeting, chapter.Name, 0), cancellation: ct);
    }
}
