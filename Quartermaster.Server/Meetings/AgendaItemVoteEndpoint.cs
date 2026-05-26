using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Meetings;
using Quartermaster.Api.Motions;
using Quartermaster.Data.ChapterAssociates;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Meetings;
using Quartermaster.Data.Motions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Meetings;

/// <summary>
/// Casts a vote on the motion linked to an agenda item, tagging the vote with the
/// meeting's ID so it's attributed to that meeting session. Delegation rules match
/// the existing <c>MotionVoteEndpoint</c>.
/// </summary>
public class AgendaItemVoteEndpoint : Endpoint<AgendaItemVoteRequest> {
    private readonly MeetingRepository _meetingRepo;
    private readonly AgendaItemRepository _agendaRepo;
    private readonly MotionRepository _motionRepo;
    private readonly ChapterOfficerRepository _officerRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly IMeetingNotifier _notifier;
    private readonly PermissionContext _perms;

    public AgendaItemVoteEndpoint(
        MeetingRepository meetingRepo,
        AgendaItemRepository agendaRepo,
        MotionRepository motionRepo,
        ChapterOfficerRepository officerRepo,
        ChapterRepository chapterRepo,
        IMeetingNotifier notifier,
        PermissionContext perms) {
        _meetingRepo = meetingRepo;
        _agendaRepo = agendaRepo;
        _motionRepo = motionRepo;
        _officerRepo = officerRepo;
        _chapterRepo = chapterRepo;
        _notifier = notifier;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/meetings/{MeetingId}/agenda/{ItemId}/vote");
    }

    public override async Task HandleAsync(AgendaItemVoteRequest req, CancellationToken ct) {
        var meeting = _meetingRepo.Get(req.MeetingId);
        if (meeting == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (meeting.Status != MeetingStatus.InProgress) {
            ThrowError(I18nKey.Error.Meeting.Agenda.VoteRequiresInProgress);
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

        var motion = _motionRepo.Get(item.MotionId.Value);
        if (motion == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var userId = _perms.UserId;
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        if (!_perms.HasGlobal(PermissionIdentifier.SystemVote) &&
            !_perms.HasGlobal(PermissionIdentifier.VoteMotions) &&
            !_perms.HasExact(motion.ChapterId, PermissionIdentifier.VoteMotions)) {
            await SendForbiddenAsync(ct);
            return;
        }

        // Delegation check — system_vote holders can vote for anyone, otherwise
        // standard delegation rules apply.
        var hasSystemVote = _perms.HasGlobal(PermissionIdentifier.SystemVote);
        if (req.UserId != userId.Value && !hasSystemVote) {
            if (!_officerRepo.IsOfficerByUserId(req.UserId, motion.ChapterId)) {
                AddError("UserId", I18nKey.Error.Meeting.Agenda.VoteTargetNotOfficer);
                await SendErrorsAsync(400, ct);
                return;
            }
            var chapterAndAncestors = _chapterRepo.GetAncestorChain(motion.ChapterId).Select(c => c.Id).ToList();
            var callerIsOfficer = _officerRepo.IsOfficerByUserIdForAnyChapter(userId.Value, chapterAndAncestors);
            if (!callerIsOfficer &&
                !_perms.Has(motion.ChapterId, PermissionIdentifier.VoteDelegateMotions)) {
                AddError("UserId", I18nKey.Error.Meeting.Agenda.VoteNoProxyPermission);
                await SendErrorsAsync(403, ct);
                return;
            }
        }

        _motionRepo.CastVote(new MotionVote {
            Id = Guid.NewGuid(),
            MotionId = motion.Id,
            UserId = req.UserId,
            Vote = (VoteType)req.Vote,
            VotedAt = DateTime.UtcNow,
            MeetingId = meeting.Id
        });

        await _notifier.NotifyAgendaItemChangedAsync(req.MeetingId, req.ItemId, "vote_cast");
        await SendOkAsync(ct);
    }
}
