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

public class AgendaItemMoveEndpoint : Endpoint<AgendaItemMoveRequest> {
    private readonly MeetingRepository _meetingRepo;
    private readonly AgendaItemRepository _agendaRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;

    public AgendaItemMoveEndpoint(
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
        Post("/api/meetings/{MeetingId}/agenda/{ItemId}/move");
    }

    public override async Task HandleAsync(AgendaItemMoveRequest req, CancellationToken ct) {
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

        if (req.NewParentId.HasValue) {
            var newParent = _agendaRepo.Get(req.NewParentId.Value);
            if (newParent == null || newParent.MeetingId != req.MeetingId) {
                ThrowError("Neuer übergeordneter Tagesordnungspunkt gehört nicht zu dieser Sitzung.");
                return;
            }
            if (_agendaRepo.WouldCreateCycle(req.ItemId, req.NewParentId.Value)) {
                ThrowError("Die Verschiebung würde einen Zyklus erzeugen.");
                return;
            }
            var newParentDepth = _agendaRepo.GetDepth(req.NewParentId.Value);
            if (newParentDepth + 1 > AgendaItemRepository.MaxDepth) {
                ThrowError($"Maximale Verschachtelungstiefe von {AgendaItemRepository.MaxDepth} überschritten.");
                return;
            }
        }

        _agendaRepo.Move(req.ItemId, req.NewParentId);
        await SendOkAsync(ct);
    }
}
