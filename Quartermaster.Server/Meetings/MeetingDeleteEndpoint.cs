using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Meetings;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Meetings;

public class MeetingDeleteRequest {
    public Guid Id { get; set; }
}

public class MeetingDeleteEndpoint : Endpoint<MeetingDeleteRequest> {
    private readonly MeetingRepository _meetingRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;

    public MeetingDeleteEndpoint(
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
        Delete("/api/meetings/{Id}");
    }

    public override async Task HandleAsync(MeetingDeleteRequest req, CancellationToken ct) {
        var meeting = _meetingRepo.Get(req.Id);
        if (meeting == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!EndpointAuthorizationHelper.HasPermission(userId.Value, meeting.ChapterId, PermissionIdentifier.DeleteMeetings, _globalPermRepo, _chapterPermRepo, _chapterRepo)) {
            await SendForbiddenAsync(ct);
            return;
        }

        _meetingRepo.SoftDelete(req.Id);
        await SendOkAsync(ct);
    }
}
