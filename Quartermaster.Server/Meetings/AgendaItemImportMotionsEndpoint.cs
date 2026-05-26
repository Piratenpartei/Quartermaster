using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Meetings;
using Quartermaster.Api.Motions;
using Quartermaster.Data.Meetings;
using Quartermaster.Data.Motions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Meetings;

public class AgendaItemImportMotionsRequest {
    public Guid MeetingId { get; set; }
    /// <summary>
    /// Optional parent item (typically a Section-type item) under which imported motions
    /// are added as subitems. If null, they are added as root agenda items.
    /// </summary>
    public Guid? ParentId { get; set; }
}

/// <summary>
/// Imports all currently open (Pending) motions for the meeting's chapter as agenda items
/// of type Motion. Skips motions that are already linked to an agenda item in this meeting.
/// Typically used on a Section-type agenda item to bulk-populate a discussion section.
/// </summary>
public class AgendaItemImportMotionsEndpoint : Endpoint<AgendaItemImportMotionsRequest> {
    private readonly MeetingRepository _meetingRepo;
    private readonly AgendaItemRepository _agendaRepo;
    private readonly MotionRepository _motionRepo;
    private readonly PermissionContext _perms;

    public AgendaItemImportMotionsEndpoint(
        MeetingRepository meetingRepo, AgendaItemRepository agendaRepo,
        MotionRepository motionRepo, PermissionContext perms) {
        _meetingRepo = meetingRepo;
        _agendaRepo = agendaRepo;
        _motionRepo = motionRepo;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/meetings/{MeetingId}/agenda/import-motions");
    }

    public override async Task HandleAsync(AgendaItemImportMotionsRequest req, CancellationToken ct) {
        var meeting = _meetingRepo.Get(req.MeetingId);
        if (meeting == null) {
            await SendNotFoundAsync(ct);
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

        // Get all pending motions for this chapter
        var (motions, _) = _motionRepo.List(meeting.ChapterId, MotionApprovalStatus.Pending, false, 1, 1000);

        // Get motion IDs already linked in this meeting's agenda
        var existingItems = _agendaRepo.GetForMeeting(meeting.Id);
        var alreadyLinked = existingItems
            .Where(a => a.MotionId.HasValue)
            .Select(a => a.MotionId!.Value)
            .ToHashSet();

        var imported = 0;
        foreach (var motion in motions) {
            if (alreadyLinked.Contains(motion.Id))
                continue;

            _agendaRepo.Create(new AgendaItem {
                MeetingId = meeting.Id,
                ParentId = req.ParentId,
                Title = motion.Title,
                ItemType = AgendaItemType.Motion,
                MotionId = motion.Id
            });
            imported++;
        }

        await SendAsync(new { Imported = imported }, cancellation: ct);
    }
}
