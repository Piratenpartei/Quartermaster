using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Meetings;
using Quartermaster.Data.Meetings;
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
    private readonly MeetingLifecycleService _lifecycle;
    private readonly IMeetingNotifier _notifier;
    private readonly PermissionContext _perms;

    public AgendaItemCloseVoteEndpoint(
        MeetingRepository meetingRepo,
        AgendaItemRepository agendaRepo,
        MeetingLifecycleService lifecycle,
        IMeetingNotifier notifier,
        PermissionContext perms) {
        _meetingRepo = meetingRepo;
        _agendaRepo = agendaRepo;
        _lifecycle = lifecycle;
        _notifier = notifier;
        _perms = perms;
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
            ThrowError(I18nKey.Error.Meeting.Agenda.CloseVoteRequiresInProgress);
            return;
        }

        var item = _agendaRepo.Get(req.ItemId);
        if (item == null || item.MeetingId != meeting.Id) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (item.ItemType != AgendaItemType.Motion || !item.MotionId.HasValue) {
            ThrowError(I18nKey.Error.Meeting.Agenda.NotMotionItem);
            return;
        }

        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.Has(meeting.ChapterId, PermissionIdentifier.EditMeetings)) {
            await SendForbiddenAsync(ct);
            return;
        }

        await _lifecycle.CloseVoteForAgendaItem(req.ItemId, ct);
        await _notifier.NotifyAgendaItemChangedAsync(req.MeetingId, req.ItemId, "vote_closed");
        await SendOkAsync(ct);
    }
}
