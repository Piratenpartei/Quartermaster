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
    private readonly PermissionContext _perms;

    public MotionDetailEndpoint(MotionRepository motionRepo, ChapterRepository chapterRepo,
        ChapterOfficerRepository officerRepo, DbContext context,
        PermissionContext perms) {
        _motionRepo = motionRepo;
        _chapterRepo = chapterRepo;
        _officerRepo = officerRepo;
        _context = context;
        _perms = perms;
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
            if (_perms.UserId == null) {
                await SendNotFoundAsync(ct);
                return;
            }
            if (!_perms.Has(motion.ChapterId, PermissionIdentifier.ViewMotions)) {
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
                MemberId = o.MemberId,
                MemberName = member != null ? $"{member.FirstName} {member.LastName}" : "Unbekannt",
                OfficerRole = o.AssociateType.ToString()
            };
        }).ToList();

        var voteDtos = votes.Select(v => {
            var member = members.FirstOrDefault(m => m.Id == v.MemberId);
            var officer = officers.FirstOrDefault(o => o.MemberId == v.MemberId);
            return new MotionVoteDTO {
                MemberId = v.MemberId,
                MemberName = member != null ? $"{member.FirstName} {member.LastName}" : "Unbekannt",
                OfficerRole = officer != null ? officer.AssociateType.ToString() : "",
                Vote = v.Vote,
                VotedAt = v.VotedAt,
                CastByUserId = v.CastByUserId
            };
        }).ToList();

        var canEdit = _perms.UserId != null && _perms.Has(motion.ChapterId, PermissionIdentifier.EditMotions);

        await SendAsync(new MotionDetailDTO {
            Id = motion.Id,
            ChapterId = motion.ChapterId,
            ChapterName = chapter?.Name ?? "",
            AuthorName = motion.AuthorName,
            AuthorEmail = motion.AuthorEmail,
            Title = motion.Title,
            Text = motion.Text,
            TextMarkdown = canEdit ? motion.TextMarkdown : null,
            IsPublic = motion.IsPublic,
            LinkedMembershipApplicationId = motion.LinkedMembershipApplicationId,
            LinkedDueSelectionId = motion.LinkedDueSelectionId,
            ApprovalStatus = motion.ApprovalStatus,
            IsRealized = motion.IsRealized,
            CreatedAt = motion.CreatedAt,
            ResolvedAt = motion.ResolvedAt,
            Votes = voteDtos,
            Officers = officerDtos,
            TotalOfficers = officers.Count
        }, cancellation: ct);
    }
}
