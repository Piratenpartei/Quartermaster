using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Motions;
using Quartermaster.Data.ChapterAssociates;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Motions;
using Quartermaster.Data.Users;

namespace Quartermaster.Server.Motions;

public class MotionDetailRequest {
    public Guid Id { get; set; }
}

public class MotionDetailEndpoint : Endpoint<MotionDetailRequest, MotionDetailDTO> {
    private readonly MotionRepository _motionRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly ChapterOfficerRepository _officerRepo;
    private readonly UserRepository _userRepo;

    public MotionDetailEndpoint(MotionRepository motionRepo, ChapterRepository chapterRepo,
        ChapterOfficerRepository officerRepo, UserRepository userRepo) {
        _motionRepo = motionRepo;
        _chapterRepo = chapterRepo;
        _officerRepo = officerRepo;
        _userRepo = userRepo;
    }

    public override void Configure() {
        Get("/api/motions/{Id}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(MotionDetailRequest req, CancellationToken ct) {
        var motion = _motionRepo.Get(req.Id);
        if (motion == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var chapter = _chapterRepo.Get(motion.ChapterId);
        var officers = _officerRepo.GetForChapter(motion.ChapterId);
        var votes = _motionRepo.GetVotes(motion.Id);

        var voteDtos = votes.Select(v => {
            var user = _userRepo.GetById(v.UserId);
            var officer = officers.FirstOrDefault(o => o.UserId == v.UserId);
            return new MotionVoteDTO {
                UserId = v.UserId,
                UserName = user != null ? $"{user.FirstName} {user.LastName}" : "Unbekannt",
                OfficerRole = officer != null ? officer.AssociateType.ToString() : "",
                Vote = (int)v.Vote,
                VotedAt = v.VotedAt
            };
        }).ToList();

        var officerDtos = officers.Select(o => {
            var user = _userRepo.GetById(o.UserId);
            return new MotionVoteDTO {
                UserId = o.UserId,
                UserName = user != null ? $"{user.FirstName} {user.LastName}" : "Unbekannt",
                OfficerRole = o.AssociateType.ToString()
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
