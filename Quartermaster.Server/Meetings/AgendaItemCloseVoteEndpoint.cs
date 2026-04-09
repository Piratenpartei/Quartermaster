using System;
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

public class AgendaItemCloseVoteRequest {
    public Guid MeetingId { get; set; }
    public Guid ItemId { get; set; }
}

/// <summary>
/// Closes voting on a Motion-type agenda item during an in-progress meeting:
/// tallies the votes, sets the motion's ApprovalStatus + ResolvedAt, and auto-fills
/// the agenda item's Resolution with the tally summary.
/// </summary>
public class AgendaItemCloseVoteEndpoint : Endpoint<AgendaItemCloseVoteRequest> {
    private readonly MeetingRepository _meetingRepo;
    private readonly AgendaItemRepository _agendaRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly MeetingLifecycleService _lifecycle;

    public AgendaItemCloseVoteEndpoint(
        MeetingRepository meetingRepo,
        AgendaItemRepository agendaRepo,
        ChapterRepository chapterRepo,
        UserChapterPermissionRepository chapterPermRepo,
        UserGlobalPermissionRepository globalPermRepo,
        MeetingLifecycleService lifecycle) {
        _meetingRepo = meetingRepo;
        _agendaRepo = agendaRepo;
        _chapterRepo = chapterRepo;
        _chapterPermRepo = chapterPermRepo;
        _globalPermRepo = globalPermRepo;
        _lifecycle = lifecycle;
    }

    public override void Configure() {
        Post("/api/meetings/{MeetingId}/agenda/{ItemId}/close-vote");
    }

    public override async Task HandleAsync(AgendaItemCloseVoteRequest req, CancellationToken ct) {
        var meeting = _meetingRepo.Get(req.MeetingId);
        if (meeting == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (meeting.Status != MeetingStatus.InProgress) {
            ThrowError("Abstimmung kann nur während laufender Sitzung geschlossen werden.");
            return;
        }

        var item = _agendaRepo.Get(req.ItemId);
        if (item == null || item.MeetingId != meeting.Id) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (item.ItemType != AgendaItemType.Motion || !item.MotionId.HasValue) {
            ThrowError("TOP ist kein Antragspunkt.");
            return;
        }

        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!EndpointAuthorizationHelper.HasPermission(userId.Value, meeting.ChapterId, PermissionIdentifier.EditMeetings, _globalPermRepo, _chapterPermRepo, _chapterRepo)) {
            await SendForbiddenAsync(ct);
            return;
        }

        _lifecycle.CloseVoteForAgendaItem(req.ItemId);
        await SendOkAsync(ct);
    }
}
