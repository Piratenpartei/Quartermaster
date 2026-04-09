using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Motions;
using Quartermaster.Data;
using Quartermaster.Data.ChapterAssociates;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Motions;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Motions;

public class MotionDetailRequest {
    public Guid Id { get; set; }
}

public class MotionDetailEndpoint : Endpoint<MotionDetailRequest, MotionDetailDTO> {
    private readonly MotionRepository _motionRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly ChapterOfficerRepository _officerRepo;
    private readonly DbContext _context;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;

    public MotionDetailEndpoint(MotionRepository motionRepo, ChapterRepository chapterRepo,
        ChapterOfficerRepository officerRepo, DbContext context,
        UserGlobalPermissionRepository globalPermRepo, UserChapterPermissionRepository chapterPermRepo) {
        _motionRepo = motionRepo;
        _chapterRepo = chapterRepo;
        _officerRepo = officerRepo;
        _context = context;
        _globalPermRepo = globalPermRepo;
        _chapterPermRepo = chapterPermRepo;
    }

    public override void Configure() {
        Get("/api/motions/{Id}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(MotionDetailRequest req, CancellationToken ct) {
        var motion = _motionRepo.Get(req.Id);
        if (motion == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (!motion.IsPublic) {
            var userId = EndpointAuthorizationHelper.GetUserId(User);
            if (userId == null) {
                await SendNotFoundAsync(ct);
                return;
            }
            if (!EndpointAuthorizationHelper.HasPermission(userId.Value, motion.ChapterId, PermissionIdentifier.ViewMotions, _globalPermRepo, _chapterPermRepo, _chapterRepo)) {
                await SendNotFoundAsync(ct);
                return;
            }
        }

        var chapter = _chapterRepo.Get(motion.ChapterId);
        var officers = _officerRepo.GetForChapter(motion.ChapterId);
        var officerMemberIds = officers.Select(o => o.MemberId).ToList();
        var members = _context.Members.Where(m => officerMemberIds.Contains(m.Id)).ToList();
        var votes = _motionRepo.GetVotes(motion.Id);

        var officerDtos = officers.Select(o => {
            var member = members.FirstOrDefault(m => m.Id == o.MemberId);
            return new MotionVoteDTO {
                UserId = member?.UserId ?? Guid.Empty,
                UserName = member != null ? $"{member.FirstName} {member.LastName}" : "Unbekannt",
                OfficerRole = o.AssociateType.ToString()
            };
        }).ToList();

        var voteDtos = votes.Select(v => {
            // Find the member that has this UserId to get the officer role
            var member = members.FirstOrDefault(m => m.UserId == v.UserId);
            var officer = member != null
                ? officers.FirstOrDefault(o => o.MemberId == member.Id)
                : null;
            return new MotionVoteDTO {
                UserId = v.UserId,
                UserName = member != null ? $"{member.FirstName} {member.LastName}" : "Unbekannt",
                OfficerRole = officer != null ? officer.AssociateType.ToString() : "",
                Vote = (int)v.Vote,
                VotedAt = v.VotedAt
            };
        }).ToList();

        await SendAsync(new MotionDetailDTO {
            Id = motion.Id,
            ChapterId = motion.ChapterId,
            ChapterName = chapter?.Name ?? "",
            AuthorName = motion.AuthorName,
            AuthorEMail = motion.AuthorEMail,
            Title = motion.Title,
            Text = motion.Text,
            IsPublic = motion.IsPublic,
            LinkedMembershipApplicationId = motion.LinkedMembershipApplicationId,
            LinkedDueSelectionId = motion.LinkedDueSelectionId,
            ApprovalStatus = (int)motion.ApprovalStatus,
            IsRealized = motion.IsRealized,
            CreatedAt = motion.CreatedAt,
            ResolvedAt = motion.ResolvedAt,
            Votes = voteDtos,
            Officers = officerDtos,
            TotalOfficers = officers.Count
        }, cancellation: ct);
    }
}
