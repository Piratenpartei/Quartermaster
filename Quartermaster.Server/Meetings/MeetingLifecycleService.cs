using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartermaster.Api.Meetings;
using Quartermaster.Api.Motions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Meetings;
using Quartermaster.Data.Motions;
using Quartermaster.Data.Options;
using Quartermaster.Server.Motions;

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
    private readonly MotionResolutionDecisionMailer _decisionMailer;
    private readonly ILogger<MeetingLifecycleService> _logger;

    public MeetingLifecycleService(
        MeetingRepository meetingRepo,
        AgendaItemRepository agendaRepo,
        MotionRepository motionRepo,
        ChapterRepository chapterRepo,
        OptionRepository optionRepo,
        MotionResolutionDecisionMailer decisionMailer,
        ILogger<MeetingLifecycleService> logger) {
        _meetingRepo = meetingRepo;
        _agendaRepo = agendaRepo;
        _motionRepo = motionRepo;
        _chapterRepo = chapterRepo;
        _optionRepo = optionRepo;
        _decisionMailer = decisionMailer;
        _logger = logger;
    }

    /// <summary>
    /// For each agenda item of type Motion with a non-null MotionId whose motion is still
    /// Pending, tally the votes and set the motion's ApprovalStatus + ResolvedAt. Also
    /// auto-fills the agenda item's Resolution field if it's empty.
    /// </summary>
    public async Task AutoResolveLinkedMotions(Guid meetingId, CancellationToken ct) {
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

            var newStatus = DetermineApprovalStatus(approve, deny, abstain);
            _motionRepo.UpdateApprovalStatus(motion.Id, newStatus);
            await _decisionMailer.NotifyAsync(motion.Id, ct);

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
    public async Task CloseVoteForAgendaItem(Guid agendaItemId, CancellationToken ct) {
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

        var newStatus = DetermineApprovalStatus(approve, deny, abstain);
        if (motion.ApprovalStatus == MotionApprovalStatus.Pending) {
            _motionRepo.UpdateApprovalStatus(motion.Id, newStatus);
            await _decisionMailer.NotifyAsync(motion.Id, ct);
        }

        _agendaRepo.UpdateResolution(item.Id, BuildResolutionText(newStatus, approve, deny, abstain));
    }

    /// <summary>
    /// Maps a vote tally to the resulting <see cref="MotionApprovalStatus"/>:
    /// no votes → <c>ClosedWithoutAction</c>; tied non-zero tally → <c>FormallyRejected</c>
    /// (German parlance: "abgelehnt durch Patt"); majority wins otherwise.
    /// </summary>
    private static MotionApprovalStatus DetermineApprovalStatus(int approve, int deny, int abstain) {
        if (approve == 0 && deny == 0 && abstain == 0)
            return MotionApprovalStatus.ClosedWithoutAction;
        if (approve > deny)
            return MotionApprovalStatus.Approved;
        if (deny > approve)
            return MotionApprovalStatus.Rejected;
        return MotionApprovalStatus.FormallyRejected;
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
            if (m != null)
                motionsById[mid] = m;
            var votes = _motionRepo.GetVotes(mid);
            voteTallies[mid] = (
                votes.Count(v => v.Vote == VoteType.Approve),
                votes.Count(v => v.Vote == VoteType.Deny),
                votes.Count(v => v.Vote == VoteType.Abstain)
            );
        }

        var itemDtos = agendaItems
            .OrderBy(a => a.ParentId.HasValue ? 1 : 0)
            .ThenBy(a => a.ParentId)
            .ThenBy(a => a.SortOrder)
            .Select(a => {
                string? motionTitle = null;
                MotionApprovalStatus? motionApprovalStatus = null;
                var approveCount = 0;
                var denyCount = 0;
                var abstainCount = 0;
                if (a.MotionId.HasValue && motionsById.TryGetValue(a.MotionId.Value, out var motion)) {
                    motionTitle = motion.Title;
                    motionApprovalStatus = motion.ApprovalStatus;
                    if (voteTallies.TryGetValue(a.MotionId.Value, out var tally)) {
                        approveCount = tally.Approve;
                        denyCount = tally.Deny;
                        abstainCount = tally.Abstain;
                    }
                }
                return new AgendaItemDTO {
                    Id = a.Id,
                    ParentId = a.ParentId,
                    SortOrder = a.SortOrder,
                    Title = a.Title,
                    ItemType = a.ItemType,
                    MotionId = a.MotionId,
                    MotionTitle = motionTitle,
                    MotionApprovalStatus = motionApprovalStatus,
                    MotionVoteApproveCount = approveCount,
                    MotionVoteDenyCount = denyCount,
                    MotionVoteAbstainCount = abstainCount,
                    Notes = a.Notes,
                    Resolution = a.Resolution,
                    StartedAt = a.StartedAt,
                    CompletedAt = a.CompletedAt
                };
            })
            .ToList();

        return new MeetingDetailDTO {
            Id = meeting.Id,
            ChapterId = meeting.ChapterId,
            ChapterName = chapterName,
            Title = meeting.Title,
            MeetingDate = meeting.MeetingDate,
            Status = meeting.Status,
            Visibility = meeting.Visibility,
            Location = meeting.Location,
            Description = meeting.Description,
            StartedAt = meeting.StartedAt,
            CompletedAt = meeting.CompletedAt,
            ArchivedPdfPath = meeting.ArchivedPdfPath,
            AgendaItems = itemDtos
        };
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
