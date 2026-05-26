using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Meetings;
using Quartermaster.Api.Motions;
using Quartermaster.Rendering;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Meetings;
using Quartermaster.Data.Motions;
using Quartermaster.Data.Options;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Data.Roles;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Meetings;

public class MeetingProtocolRequest {
    public Guid Id { get; set; }
    public string Format { get; set; } = "html";
    public bool Draft { get; set; }
}

/// <summary>
/// Exports a meeting's protocol. Formats: md / html / pdf. Archived meetings stream
/// their frozen PDF snapshot (if pdf requested); Completed meetings regenerate.
/// Draft/Scheduled/InProgress meetings require draft=true (preview).
/// </summary>
public class MeetingProtocolEndpoint : Endpoint<MeetingProtocolRequest> {
    private readonly MeetingRepository _meetingRepo;
    private readonly AgendaItemRepository _agendaRepo;
    private readonly MotionRepository _motionRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly OptionRepository _optionRepo;
    private readonly RoleRepository _roleRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;

    public MeetingProtocolEndpoint(
        MeetingRepository meetingRepo,
        AgendaItemRepository agendaRepo,
        MotionRepository motionRepo,
        ChapterRepository chapterRepo,
        OptionRepository optionRepo,
        RoleRepository roleRepo,
        UserGlobalPermissionRepository globalPermRepo,
        UserChapterPermissionRepository chapterPermRepo) {
        _meetingRepo = meetingRepo;
        _agendaRepo = agendaRepo;
        _motionRepo = motionRepo;
        _chapterRepo = chapterRepo;
        _optionRepo = optionRepo;
        _roleRepo = roleRepo;
        _globalPermRepo = globalPermRepo;
        _chapterPermRepo = chapterPermRepo;
    }

    public override void Configure() {
        Get("/api/meetings/{Id}/protocol");
        AllowAnonymous();
    }

    public override async Task HandleAsync(MeetingProtocolRequest req, CancellationToken ct) {
        var meeting = _meetingRepo.Get(req.Id);
        if (meeting == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (!MeetingAccessHelper.CanUserViewMeeting(userId, meeting, _roleRepo, _globalPermRepo, _chapterPermRepo, _chapterRepo)) {
            await SendNotFoundAsync(ct); // 404, not 403 — don't leak private meeting existence
            return;
        }

        // Only Completed/Archived are exportable by default; draft=true allows preview.
        if (meeting.Status != MeetingStatus.Completed && meeting.Status != MeetingStatus.Archived && !req.Draft) {
            ThrowError(I18nKey.Error.Meeting.ProtocolNotAvailable);
            return;
        }

        var format = (req.Format ?? "html").ToLowerInvariant();

        // Archived + pdf → stream the frozen snapshot if it exists.
        if (format == "pdf" && meeting.Status == MeetingStatus.Archived && !string.IsNullOrWhiteSpace(meeting.ArchivedPdfPath)) {
            var archiveDir = _optionRepo.GetGlobalValue("meetings.protocol.archive_dir")?.Value;
            if (string.IsNullOrWhiteSpace(archiveDir))
                archiveDir = Path.Combine(AppContext.BaseDirectory, "data", "protocols");
            var fullPath = Path.Combine(archiveDir, meeting.ArchivedPdfPath);
            if (File.Exists(fullPath)) {
                var bytes = await File.ReadAllBytesAsync(fullPath, ct);
                await SendBytesAsync(bytes, fileName: $"sitzung-{meeting.Id}.pdf",
                    contentType: "application/pdf", cancellation: ct);
                return;
            }
            // Fall through to regeneration if snapshot is missing.
        }

        // Build the DTO fresh.
        var detail = BuildDetail(meeting);

        if (format == "md") {
            var markdown = ProtocolRenderer.RenderMarkdown(detail);
            await SendStringAsync(markdown, contentType: "text/markdown; charset=utf-8", cancellation: ct);
            return;
        }
        if (format == "html") {
            var markdown = ProtocolRenderer.RenderMarkdown(detail);
            var html = MarkdownService.ToHtml(markdown, SanitizationProfile.Standard);
            await SendStringAsync(html, contentType: "text/html; charset=utf-8", cancellation: ct);
            return;
        }
        if (format == "pdf") {
            var pdf = ProtocolPdfRenderer.Render(detail);
            await SendBytesAsync(pdf, fileName: $"sitzung-{meeting.Id}.pdf",
                contentType: "application/pdf", cancellation: ct);
            return;
        }

        ThrowError(I18nParams.With(I18nKey.Error.Meeting.ProtocolUnknownFormat, ("format", format)));
    }

    private MeetingDetailDTO BuildDetail(Meeting meeting) {
        var agendaItems = _agendaRepo.GetForMeeting(meeting.Id);
        var chapterName = _chapterRepo.Get(meeting.ChapterId)?.Name ?? "";

        var motionsById = new Dictionary<Guid, Motion>();
        var voteTallies = new Dictionary<Guid, (int Approve, int Deny, int Abstain)>();
        foreach (var mid in agendaItems
                     .Where(a => a.MotionId.HasValue)
                     .Select(a => a.MotionId!.Value)
                     .Distinct()) {
            var m = _motionRepo.Get(mid);
            if (m != null) {
                motionsById[mid] = m;
            }
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
}
