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

public class AgendaItemUpdateEndpoint : Endpoint<AgendaItemUpdateRequest> {
    private readonly MeetingRepository _meetingRepo;
    private readonly AgendaItemRepository _agendaRepo;
    private readonly MotionRepository _motionRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;

    public AgendaItemUpdateEndpoint(
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
        Put("/api/meetings/{MeetingId}/agenda/{ItemId}");
    }

    public override async Task HandleAsync(AgendaItemUpdateRequest req, CancellationToken ct) {
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
        if (!EndpointAuthorizationHelper.HasPermission(userId.Value, meeting.ChapterId, PermissionIdentifier.EditMeetings, _globalPermRepo, _chapterPermRepo, _chapterRepo)) {
            await SendForbiddenAsync(ct);
            return;
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

        var updated = new AgendaItem {
            Id = req.ItemId,
            Title = req.Title,
            ItemType = req.ItemType,
            MotionId = req.MotionId,
            Notes = req.Notes,
            Resolution = req.Resolution
        };
        _agendaRepo.Update(updated);
        await SendOkAsync(ct);
    }
}
