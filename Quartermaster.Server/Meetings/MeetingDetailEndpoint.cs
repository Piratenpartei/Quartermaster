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
    private readonly DbContext _db;

    public MeetingDetailEndpoint(
        MeetingRepository meetingRepo,
        AgendaItemRepository agendaRepo,
        ChapterRepository chapterRepo,
        RoleRepository roleRepo,
        DbContext db) {
        _meetingRepo = meetingRepo;
        _agendaRepo = agendaRepo;
        _chapterRepo = chapterRepo;
        _roleRepo = roleRepo;
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
        if (!MeetingAccessHelper.CanUserViewMeeting(userId, meeting, _roleRepo)) {
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

        var dto = MeetingDtoBuilder.BuildMeetingDetailDTO(
            meeting,
            chapter?.Name ?? "",
            agendaItems,
            motionsById,
            voteTallies);

        await SendAsync(dto, cancellation: ct);
    }
}
