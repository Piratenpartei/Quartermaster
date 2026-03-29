using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Motions;
using Quartermaster.Data.ChapterAssociates;
using Quartermaster.Data.Motions;

namespace Quartermaster.Server.Motions;

public class MotionVoteEndpoint : Endpoint<MotionVoteRequest> {
    private readonly MotionRepository _motionRepo;
    private readonly ChapterOfficerRepository _officerRepo;

    public MotionVoteEndpoint(MotionRepository motionRepo, ChapterOfficerRepository officerRepo) {
        _motionRepo = motionRepo;
        _officerRepo = officerRepo;
    }

    public override void Configure() {
        Post("/api/motions/vote");
        AllowAnonymous();
    }

    public override async Task HandleAsync(MotionVoteRequest req, CancellationToken ct) {
        var motion = _motionRepo.Get(req.MotionId);
        if (motion == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var vote = (VoteType)req.Vote;
        _motionRepo.CastVote(new MotionVote {
            MotionId = req.MotionId,
            UserId = req.UserId,
            Vote = vote,
            VotedAt = DateTime.UtcNow
        });

        _motionRepo.TryAutoResolve(req.MotionId, _officerRepo);

        await SendOkAsync(ct);
    }
}
