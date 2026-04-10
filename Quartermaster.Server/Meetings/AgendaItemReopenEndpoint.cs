using System;
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

public class AgendaItemReopenRequest {
    public Guid MeetingId { get; set; }
    public Guid ItemId { get; set; }
}

/// <summary>
/// Resets an agenda item's CompletedAt to null, effectively re-opening it.
/// Meetings sometimes jump around — this lets the minute-taker undo a premature completion.
/// </summary>
public class AgendaItemReopenEndpoint : Endpoint<AgendaItemReopenRequest> {
    private readonly MeetingRepository _meetingRepo;
    private readonly AgendaItemRepository _agendaRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;

    public AgendaItemReopenEndpoint(
        MeetingRepository meetingRepo, AgendaItemRepository agendaRepo,
        ChapterRepository chapterRepo, UserChapterPermissionRepository chapterPermRepo,
        UserGlobalPermissionRepository globalPermRepo) {
        _meetingRepo = meetingRepo;
        _agendaRepo = agendaRepo;
        _chapterRepo = chapterRepo;
        _chapterPermRepo = chapterPermRepo;
        _globalPermRepo = globalPermRepo;
    }

    public override void Configure() {
        Post("/api/meetings/{MeetingId}/agenda/{ItemId}/reopen");
    }

    public override async Task HandleAsync(AgendaItemReopenRequest req, CancellationToken ct) {
        var meeting = _meetingRepo.Get(req.MeetingId);
        if (meeting == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (meeting.Status != MeetingStatus.InProgress) {
            ThrowError(I18nKey.Error.Meeting.Agenda.ReopenRequiresInProgress);
            return;
        }
        var item = _agendaRepo.Get(req.ItemId);
        if (item == null || item.MeetingId != meeting.Id) {
            await SendNotFoundAsync(ct);
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

        _agendaRepo.ResetCompletion(req.ItemId);
        await SendOkAsync(ct);
    }
}
