using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using LinqToDB;
using Quartermaster.Api.Meetings;
using Quartermaster.Data;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Meetings;
using Quartermaster.Data.Motions;
using Quartermaster.Data.Roles;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Meetings;

public class MeetingDetailRequest {
    public Guid Id { get; set; }
}

public class MeetingDetailEndpoint : Endpoint<MeetingDetailRequest, MeetingDetailDTO> {
    private readonly MeetingRepository _meetingRepo;
    private readonly AgendaItemRepository _agendaRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly RoleRepository _roleRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly DbContext _db;

    public MeetingDetailEndpoint(
        MeetingRepository meetingRepo,
        AgendaItemRepository agendaRepo,
        ChapterRepository chapterRepo,
        RoleRepository roleRepo,
        UserGlobalPermissionRepository globalPermRepo,
        UserChapterPermissionRepository chapterPermRepo,
        DbContext db) {
        _meetingRepo = meetingRepo;
        _agendaRepo = agendaRepo;
        _chapterRepo = chapterRepo;
        _roleRepo = roleRepo;
        _globalPermRepo = globalPermRepo;
        _chapterPermRepo = chapterPermRepo;
        _db = db;
    }

    public override void Configure() {
        Get("/api/meetings/{Id}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(MeetingDetailRequest req, CancellationToken ct) {
        var meeting = _meetingRepo.Get(req.Id);
        if (meeting == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (!MeetingAccessHelper.CanUserViewMeeting(userId, meeting, _roleRepo, _globalPermRepo, _chapterPermRepo, _chapterRepo)) {
            // Don't leak existence of private meetings
            await SendNotFoundAsync(ct);
            return;
        }

        var chapter = _chapterRepo.Get(meeting.ChapterId);
        var agendaItems = _agendaRepo.GetForMeeting(meeting.Id);

        var motionIds = agendaItems
            .Where(a => a.MotionId.HasValue)
            .Select(a => a.MotionId!.Value)
            .Distinct()
            .ToList();

        var motionsById = _db.Motions
            .Where(m => motionIds.Contains(m.Id) && m.DeletedAt == null)
            .ToDictionary(m => m.Id, m => m);

        var voteTallies = _db.MotionVotes
            .Where(v => motionIds.Contains(v.MotionId))
            .ToList()
            .GroupBy(v => v.MotionId)
            .ToDictionary(
                g => g.Key,
                g => (
                    Approve: g.Count(v => v.Vote == VoteType.Approve),
                    Deny: g.Count(v => v.Vote == VoteType.Deny),
                    Abstain: g.Count(v => v.Vote == VoteType.Abstain)
                ));

        // Build per-officer vote lists for Motion and Presence agenda items.
        var officers = _db.ChapterOfficers
            .Where(o => o.ChapterId == meeting.ChapterId)
            .ToList();
        var officerMemberIds = officers.Select(o => o.MemberId).ToList();
        var officerMembers = _db.Members
            .Where(m => officerMemberIds.Contains(m.Id))
            .ToList();
        var allVotes = motionIds.Count > 0
            ? _db.MotionVotes.Where(v => motionIds.Contains(v.MotionId)).ToList()
            : new List<Data.Motions.MotionVote>();

        var itemDtos = agendaItems
            .OrderBy(a => a.ParentId.HasValue ? 1 : 0)
            .ThenBy(a => a.ParentId)
            .ThenBy(a => a.SortOrder)
            .Select(a => {
                List<AgendaItemOfficerVoteDTO>? officerVotes = null;
                if (a.ItemType == Api.Meetings.AgendaItemType.Motion && a.MotionId.HasValue) {
                    var motionVotes = allVotes.Where(v => v.MotionId == a.MotionId.Value).ToList();
                    officerVotes = officers.Select(o => {
                        var member = officerMembers.FirstOrDefault(m => m.Id == o.MemberId);
                        var vote = member?.UserId != null
                            ? motionVotes.FirstOrDefault(v => v.UserId == member.UserId.Value)
                            : null;
                        return new AgendaItemOfficerVoteDTO {
                            UserId = member?.UserId ?? Guid.Empty,
                            UserName = member != null ? $"{member.FirstName} {member.LastName}" : "Unbekannt",
                            OfficerRole = o.AssociateType.ToString(),
                            Vote = vote != null ? (int?)vote.Vote : null
                        };
                    }).ToList();
                } else if (a.ItemType == Api.Meetings.AgendaItemType.Presence) {
                    // Presence stored as JSON array of user ID strings in Resolution field
                    var presentIds = new HashSet<string>();
                    if (!string.IsNullOrWhiteSpace(a.Resolution)) {
                        try {
                            var parsed = System.Text.Json.JsonSerializer.Deserialize<List<string>>(a.Resolution);
                            if (parsed != null)
                                presentIds = new HashSet<string>(parsed);
                        } catch { }
                    }
                    officerVotes = officers.Select(o => {
                        var member = officerMembers.FirstOrDefault(m => m.Id == o.MemberId);
                        var odUserId = member?.UserId ?? Guid.Empty;
                        return new AgendaItemOfficerVoteDTO {
                            UserId = odUserId,
                            UserName = member != null ? $"{member.FirstName} {member.LastName}" : "Unbekannt",
                            OfficerRole = o.AssociateType.ToString(),
                            IsPresent = presentIds.Contains(odUserId.ToString())
                        };
                    }).ToList();
                }
                return MeetingDtoBuilder.BuildAgendaItemDTO(a, motionsById, voteTallies, officerVotes);
            })
            .ToList();

        var dto = new MeetingDetailDTO {
            Id = meeting.Id,
            ChapterId = meeting.ChapterId,
            ChapterName = chapter?.Name ?? "",
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

        await SendAsync(dto, cancellation: ct);
    }
}
