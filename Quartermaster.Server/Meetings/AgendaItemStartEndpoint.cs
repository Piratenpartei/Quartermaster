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

public class AgendaItemStartRequest {
    public Guid MeetingId { get; set; }
    public Guid ItemId { get; set; }
}

public class AgendaItemStartEndpoint : Endpoint<AgendaItemStartRequest> {
    private readonly MeetingRepository _meetingRepo;
    private readonly AgendaItemRepository _agendaRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;

    public AgendaItemStartEndpoint(
        MeetingRepository meetingRepo,
        AgendaItemRepository agendaRepo,
        ChapterRepository chapterRepo,
        UserGlobalPermissionRepository globalPermRepo,
        UserChapterPermissionRepository chapterPermRepo) {
        _meetingRepo = meetingRepo;
        _agendaRepo = agendaRepo;
        _chapterRepo = chapterRepo;
        _globalPermRepo = globalPermRepo;
        _chapterPermRepo = chapterPermRepo;
    }

    public override void Configure() {
        Post("/api/meetings/{MeetingId}/agenda/{ItemId}/start");
    }

    public override async Task HandleAsync(AgendaItemStartRequest req, CancellationToken ct) {
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

        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, PermissionIdentifier.EditMeetings, _globalPermRepo) &&
            !_chapterPermRepo.HasPermissionWithInheritance(userId.Value, meeting.ChapterId, PermissionIdentifier.EditMeetings, _chapterRepo)) {
            await SendForbiddenAsync(ct);
            return;
        }

        if (meeting.Status != MeetingStatus.InProgress) {
            ThrowError("Tagesordnungspunkte können nur während einer laufenden Sitzung gestartet werden.");
            return;
        }

        _agendaRepo.CompleteAllInProgressExcept(req.MeetingId, req.ItemId);
        _agendaRepo.MarkStarted(req.ItemId);
        await SendOkAsync(ct);
    }
}
