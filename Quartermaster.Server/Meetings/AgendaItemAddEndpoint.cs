using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
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
        if (!EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, PermissionIdentifier.EditMeetings, _globalPermRepo) &&
            !_chapterPermRepo.HasPermissionWithInheritance(userId.Value, meeting.ChapterId, PermissionIdentifier.EditMeetings, _chapterRepo)) {
            await SendForbiddenAsync(ct);
            return;
        }

        if (req.ParentId.HasValue) {
            var parent = _agendaRepo.Get(req.ParentId.Value);
            if (parent == null || parent.MeetingId != req.MeetingId) {
                ThrowError("Übergeordneter Tagesordnungspunkt gehört nicht zu dieser Sitzung.");
                return;
            }
            var parentDepth = _agendaRepo.GetDepth(parent.Id);
            if (parentDepth + 1 > AgendaItemRepository.MaxDepth) {
                ThrowError($"Maximale Verschachtelungstiefe von {AgendaItemRepository.MaxDepth} überschritten.");
                return;
            }
        }

        if (req.ItemType == AgendaItemType.Motion) {
            if (!req.MotionId.HasValue) {
                ThrowError("Für Antragspunkte muss ein Antrag verknüpft werden.");
                return;
            }
            var motion = _motionRepo.Get(req.MotionId.Value);
            if (motion == null) {
                ThrowError("Verknüpfter Antrag wurde nicht gefunden.");
                return;
            }
            if (motion.ChapterId != meeting.ChapterId) {
                ThrowError("Der Antrag gehört nicht zur selben Gliederung wie die Sitzung.");
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
