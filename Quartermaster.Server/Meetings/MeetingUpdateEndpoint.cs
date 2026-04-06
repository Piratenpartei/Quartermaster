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

public class MeetingUpdateEndpoint : Endpoint<MeetingUpdateRequest> {
    private readonly MeetingRepository _meetingRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;

    public MeetingUpdateEndpoint(
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
        Put("/api/meetings/{Id}");
    }

    public override async Task HandleAsync(MeetingUpdateRequest req, CancellationToken ct) {
        var existing = _meetingRepo.Get(req.Id);
        if (existing == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, PermissionIdentifier.EditMeetings, _globalPermRepo) &&
            !_chapterPermRepo.HasPermissionWithInheritance(userId.Value, existing.ChapterId, PermissionIdentifier.EditMeetings, _chapterRepo)) {
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
