using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Meetings;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Meetings;
using Quartermaster.Data.Motions;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Meetings;

public class AgendaItemAddEndpoint : Endpoint<AgendaItemCreateRequest, AgendaItemDTO> {
    private readonly MeetingRepository _meetingRepo;
    private readonly AgendaItemRepository _agendaRepo;
    private readonly MotionRepository _motionRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;

    public AgendaItemAddEndpoint(
        MeetingRepository meetingRepo,
        AgendaItemRepository agendaRepo,
        MotionRepository motionRepo,
        ChapterRepository chapterRepo,
        UserGlobalPermissionRepository globalPermRepo,
        UserChapterPermissionRepository chapterPermRepo) {
        _meetingRepo = meetingRepo;
        _agendaRepo = agendaRepo;
        _motionRepo = motionRepo;
        _chapterRepo = chapterRepo;
        _globalPermRepo = globalPermRepo;
        _chapterPermRepo = chapterPermRepo;
    }

    public override void Configure() {
        Post("/api/meetings/{MeetingId}/agenda");
    }

    public override async Task HandleAsync(AgendaItemCreateRequest req, CancellationToken ct) {
        var meeting = _meetingRepo.Get(req.MeetingId);
        if (meeting == null) {
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

        if (req.ParentId.HasValue) {
            var parent = _agendaRepo.Get(req.ParentId.Value);
            if (parent == null || parent.MeetingId != req.MeetingId) {
                ThrowError(I18nKey.Error.Meeting.Agenda.ParentNotInMeeting);
                return;
            }
            var parentDepth = _agendaRepo.GetDepth(parent.Id);
            if (parentDepth + 1 > AgendaItemRepository.MaxDepth) {
                ThrowError(I18nParams.With(I18nKey.Error.Meeting.Agenda.MaxDepthExceeded,
                    ("maxDepth", AgendaItemRepository.MaxDepth.ToString())));
                return;
            }
        }

        if (req.ItemType == AgendaItemType.Motion) {
            if (!req.MotionId.HasValue) {
                ThrowError(I18nKey.Error.Meeting.Agenda.MotionLinkRequired);
                return;
            }
            var motion = _motionRepo.Get(req.MotionId.Value);
            if (motion == null) {
                ThrowError(I18nKey.Error.Meeting.Agenda.LinkedMotionNotFound);
                return;
            }
            if (motion.ChapterId != meeting.ChapterId) {
                ThrowError(I18nKey.Error.Meeting.Agenda.MotionChapterMismatch);
                return;
            }
        }

        var item = new AgendaItem {
            MeetingId = req.MeetingId,
            ParentId = req.ParentId,
            Title = req.Title,
            ItemType = req.ItemType,
            MotionId = req.MotionId
        };
        _agendaRepo.Create(item);

        await SendAsync(new AgendaItemDTO {
            Id = item.Id,
            ParentId = item.ParentId,
            SortOrder = item.SortOrder,
            Title = item.Title,
            ItemType = item.ItemType,
            MotionId = item.MotionId
        }, cancellation: ct);
    }
}
