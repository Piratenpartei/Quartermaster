using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Motions;
using Quartermaster.Api.Rendering;
using Quartermaster.Data.Motions;

namespace Quartermaster.Server.Motions;

public class MotionCreateEndpoint : Endpoint<MotionCreateRequest, MotionDTO> {
    private readonly MotionRepository _motionRepo;

    public MotionCreateEndpoint(MotionRepository motionRepo) {
        _motionRepo = motionRepo;
    }

    public override void Configure() {
        Post("/api/motions");
        AllowAnonymous();
    }

    public override async Task HandleAsync(MotionCreateRequest req, CancellationToken ct) {
        var motion = new Motion {
            ChapterId = req.ChapterId,
            AuthorName = req.AuthorName,
            AuthorEMail = req.AuthorEMail,
            Title = req.Title,
            Text = MarkdownService.ToHtml(req.Text, SanitizationProfile.Strict),
            IsPublic = true,
            ApprovalStatus = MotionApprovalStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _motionRepo.Create(motion);

        await SendAsync(new MotionDTO {
            Id = motion.Id,
            ChapterId = motion.ChapterId,
            AuthorName = motion.AuthorName,
            Title = motion.Title,
            IsPublic = true,
            ApprovalStatus = 0,
            CreatedAt = motion.CreatedAt
        }, cancellation: ct);
    }
}
