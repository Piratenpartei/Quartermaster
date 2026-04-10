using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Meetings;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Meetings;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Meetings;

public class MeetingStatusUpdateEndpoint : Endpoint<MeetingStatusUpdateRequest> {
    private readonly MeetingRepository _meetingRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly MeetingLifecycleService _lifecycle;

    public MeetingStatusUpdateEndpoint(
        MeetingRepository meetingRepo,
        ChapterRepository chapterRepo,
        UserGlobalPermissionRepository globalPermRepo,
        UserChapterPermissionRepository chapterPermRepo,
        MeetingLifecycleService lifecycle) {
        _meetingRepo = meetingRepo;
        _chapterRepo = chapterRepo;
        _globalPermRepo = globalPermRepo;
        _chapterPermRepo = chapterPermRepo;
        _lifecycle = lifecycle;
    }

    public override void Configure() {
        Put("/api/meetings/{Id}/status");
    }

    public override async Task HandleAsync(MeetingStatusUpdateRequest req, CancellationToken ct) {
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

        // Archive transitions require DeleteMeetings; others require EditMeetings.
        var isArchiveTransition = req.Status == MeetingStatus.Archived || meeting.Status == MeetingStatus.Archived;
        var requiredPerm = isArchiveTransition
            ? PermissionIdentifier.DeleteMeetings
            : PermissionIdentifier.EditMeetings;

        if (!EndpointAuthorizationHelper.HasPermission(userId.Value, meeting.ChapterId, requiredPerm, _globalPermRepo, _chapterPermRepo, _chapterRepo)) {
            await SendForbiddenAsync(ct);
            return;
        }

        if (!IsTransitionAllowed(meeting.Status, req.Status)) {
            ThrowError(I18nParams.With(I18nKey.Error.Meeting.Status.TransitionInvalid,
                ("from", meeting.Status.ToString()),
                ("to", req.Status.ToString())));
            return;
        }

        if (req.Status == MeetingStatus.Scheduled && !meeting.MeetingDate.HasValue) {
            ThrowError(I18nKey.Error.Meeting.Status.DateRequiredForScheduled);
            return;
        }

        _meetingRepo.UpdateStatus(req.Id, req.Status);

        // Completed transition: auto-resolve any linked motions that weren't closed manually.
        if (req.Status == MeetingStatus.Completed && meeting.Status == MeetingStatus.InProgress)
            _lifecycle.AutoResolveLinkedMotions(req.Id);

        // Archived transition from Completed: generate immutable PDF snapshot.
        if (req.Status == MeetingStatus.Archived && meeting.Status == MeetingStatus.Completed)
            _lifecycle.GenerateAndStoreArchivePdf(req.Id);

        await SendOkAsync(ct);
    }

    private static bool IsTransitionAllowed(MeetingStatus from, MeetingStatus to) {
        if (from == to)
            return false;
        return (from, to) switch {
            (MeetingStatus.Draft, MeetingStatus.Scheduled) => true,
            (MeetingStatus.Scheduled, MeetingStatus.Draft) => true,
            (MeetingStatus.Scheduled, MeetingStatus.InProgress) => true,
            (MeetingStatus.InProgress, MeetingStatus.Completed) => true,
            (MeetingStatus.Completed, MeetingStatus.InProgress) => true,
            (MeetingStatus.Completed, MeetingStatus.Archived) => true,
            (MeetingStatus.Archived, MeetingStatus.Completed) => true,
            _ => false
        };
    }
}
