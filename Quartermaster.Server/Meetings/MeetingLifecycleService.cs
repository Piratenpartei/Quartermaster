using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Quartermaster.Api.Meetings;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Meetings;
using Quartermaster.Data.Motions;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.Meetings;

/// <summary>
/// Side effects tied to the Meeting status transitions:
/// - Completed: auto-resolve linked motions that haven't been resolved yet.
/// - Archived: render the protocol as PDF and write it to disk (immutable snapshot).
/// </summary>
public class MeetingLifecycleService {
    private readonly MeetingRepository _meetingRepo;
    private readonly AgendaItemRepository _agendaRepo;
    private readonly MotionRepository _motionRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly OptionRepository _optionRepo;
    private readonly ILogger<MeetingLifecycleService> _logger;

    public MeetingLifecycleService(
        MeetingRepository meetingRepo,
        AgendaItemRepository agendaRepo,
        MotionRepository motionRepo,
        ChapterRepository chapterRepo,
        OptionRepository optionRepo,
        ILogger<MeetingLifecycleService> logger) {
        _meetingRepo = meetingRepo;
        _agendaRepo = agendaRepo;
        _motionRepo = motionRepo;
        _chapterRepo = chapterRepo;
        _optionRepo = optionRepo;
        _logger = logger;
    }

    /// <summary>
    /// For each agenda item of type Motion with a non-null MotionId whose motion is still
    /// Pending, tally the votes and set the motion's ApprovalStatus + ResolvedAt. Also
    /// auto-fills the agenda item's Resolution field if it's empty.
    /// </summary>
    public void AutoResolveLinkedMotions(Guid meetingId) {
        var items = _agendaRepo.GetForMeeting(meetingId)
            .Where(a => a.ItemType == AgendaItemType.Motion && a.MotionId.HasValue)
            .ToList();

        foreach (var item in items) {
            var motion = _motionRepo.Get(item.MotionId!.Value);
            if (motion == null || motion.ApprovalStatus != MotionApprovalStatus.Pending)
                continue;

            var votes = _motionRepo.GetVotes(motion.Id);
            var approve = votes.Count(v => v.Vote == VoteType.Approve);
            var deny = votes.Count(v => v.Vote == VoteType.Deny);
            var abstain = votes.Count(v => v.Vote == VoteType.Abstain);

            MotionApprovalStatus newStatus;
            if (approve == 0 && deny == 0 && abstain == 0) {
                // No votes cast — mark as closed without action rather than approved/rejected.
                newStatus = MotionApprovalStatus.ClosedWithoutAction;
            } else if (approve > deny) {
                newStatus = MotionApprovalStatus.Approved;
            } else if (deny > approve) {
                newStatus = MotionApprovalStatus.Rejected;
            } else {
                // Tied — formally rejected (German parlance: "abgelehnt durch Patt").
                newStatus = MotionApprovalStatus.FormallyRejected;
            }

            _motionRepo.UpdateApprovalStatus(motion.Id, newStatus);

            if (string.IsNullOrWhiteSpace(item.Resolution)) {
                var resolutionText = BuildResolutionText(newStatus, approve, deny, abstain);
                _agendaRepo.UpdateResolution(item.Id, resolutionText);
            }
        }
    }

    /// <summary>
    /// Tally votes for a single agenda item's linked motion, close the motion, and fill
    /// the item's Resolution field. Used by the explicit close-vote endpoint.
    /// </summary>
    public void CloseVoteForAgendaItem(Guid agendaItemId) {
        var item = _agendaRepo.Get(agendaItemId);
        if (item == null || item.ItemType != AgendaItemType.Motion || !item.MotionId.HasValue)
            return;
        var motion = _motionRepo.Get(item.MotionId.Value);
        if (motion == null)
            return;

        var votes = _motionRepo.GetVotes(motion.Id);
        var approve = votes.Count(v => v.Vote == VoteType.Approve);
        var deny = votes.Count(v => v.Vote == VoteType.Deny);
        var abstain = votes.Count(v => v.Vote == VoteType.Abstain);

        MotionApprovalStatus newStatus;
        if (approve == 0 && deny == 0 && abstain == 0) {
            newStatus = MotionApprovalStatus.ClosedWithoutAction;
        } else if (approve > deny) {
            newStatus = MotionApprovalStatus.Approved;
        } else if (deny > approve) {
            newStatus = MotionApprovalStatus.Rejected;
        } else {
            newStatus = MotionApprovalStatus.FormallyRejected;
        }

        if (motion.ApprovalStatus == MotionApprovalStatus.Pending)
            _motionRepo.UpdateApprovalStatus(motion.Id, newStatus);

        _agendaRepo.UpdateResolution(item.Id, BuildResolutionText(newStatus, approve, deny, abstain));
    }

    /// <summary>
    /// Generates a PDF protocol for the meeting and writes it to
    /// {meetings.protocol.archive_dir}/{year}/{meeting_id}.pdf. Returns the relative path
    /// stored on the meeting record.
    /// </summary>
    public string GenerateAndStoreArchivePdf(Guid meetingId) {
        var meeting = _meetingRepo.Get(meetingId)
            ?? throw new InvalidOperationException($"Meeting {meetingId} not found for PDF export");

        var detail = BuildDetailDtoForRender(meeting);

        var archiveDir = _optionRepo.GetGlobalValue("meetings.protocol.archive_dir")?.Value;
        if (string.IsNullOrWhiteSpace(archiveDir))
            archiveDir = Path.Combine(AppContext.BaseDirectory, "data", "protocols");

        var year = (detail.MeetingDate ?? detail.StartedAt ?? DateTime.UtcNow).Year;
        var dir = Path.Combine(archiveDir, year.ToString());
        Directory.CreateDirectory(dir);

        var filename = $"{meetingId}.pdf";
        var fullPath = Path.Combine(dir, filename);
        var relPath = Path.Combine(year.ToString(), filename);

        var bytes = ProtocolPdfRenderer.Render(detail);
        File.WriteAllBytes(fullPath, bytes);

        _logger.LogInformation("Wrote meeting protocol PDF: {Path} ({Size} bytes)", fullPath, bytes.Length);
        _meetingRepo.SetArchivedPdfPath(meetingId, relPath);
        return relPath;
    }

    private MeetingDetailDTO BuildDetailDtoForRender(Meeting meeting) {
        var agendaItems = _agendaRepo.GetForMeeting(meeting.Id);
        var chapterName = _chapterRepo.Get(meeting.ChapterId)?.Name ?? "";

        var motionIds = agendaItems.Where(a => a.MotionId.HasValue).Select(a => a.MotionId!.Value).Distinct().ToList();
        var motionsById = new Dictionary<Guid, Motion>();
        var voteTallies = new Dictionary<Guid, (int Approve, int Deny, int Abstain)>();
        foreach (var mid in motionIds) {
            var m = _motionRepo.Get(mid);
            if (m != null) motionsById[mid] = m;
            var votes = _motionRepo.GetVotes(mid);
            voteTallies[mid] = (
                votes.Count(v => v.Vote == VoteType.Approve),
                votes.Count(v => v.Vote == VoteType.Deny),
                votes.Count(v => v.Vote == VoteType.Abstain)
            );
        }

        return MeetingDtoBuilder.BuildMeetingDetailDTO(meeting, chapterName, agendaItems, motionsById, voteTallies);
    }

    private static string BuildResolutionText(MotionApprovalStatus status, int approve, int deny, int abstain) {
        var label = status switch {
            MotionApprovalStatus.Approved => "Angenommen",
            MotionApprovalStatus.Rejected => "Abgelehnt",
            MotionApprovalStatus.FormallyRejected => "Formell abgelehnt",
            MotionApprovalStatus.ClosedWithoutAction => "Ohne Abstimmung geschlossen",
            _ => status.ToString()
        };
        return $"{label} mit {approve} Ja / {deny} Nein / {abstain} Enthaltungen";
    }
}
