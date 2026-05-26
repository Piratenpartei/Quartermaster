using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Motions;
using Quartermaster.Data.ChapterAssociates;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Motions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Motions;

public class MotionVoteEndpoint : Endpoint<MotionVoteRequest> {
    private readonly MotionRepository _motionRepo;
    private readonly ChapterOfficerRepository _officerRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly PermissionContext _perms;

    public MotionVoteEndpoint(MotionRepository motionRepo, ChapterOfficerRepository officerRepo,
        ChapterRepository chapterRepo, PermissionContext perms) {
        _motionRepo = motionRepo;
        _officerRepo = officerRepo;
        _chapterRepo = chapterRepo;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/motions/vote");
    }

    public override async Task HandleAsync(MotionVoteRequest req, CancellationToken ct) {
        var motion = _motionRepo.Get(req.MotionId);
        if (motion == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        if (!_perms.HasGlobal(PermissionIdentifier.SystemVote) &&
            !_perms.HasGlobal(PermissionIdentifier.VoteMotions) &&
            !_perms.HasExact(motion.ChapterId, PermissionIdentifier.VoteMotions)) {
            await SendForbiddenAsync(ct);
            return;
        }

        // Delegation: voting on behalf of another user requires additional checks.
        // system_vote holders can vote for anyone without delegation checks.
        var hasSystemVote = _perms.HasGlobal(PermissionIdentifier.SystemVote);
        if (req.UserId != _perms.UserId.Value && !hasSystemVote) {
            // Target must be a chapter officer of the motion's chapter
            if (!_officerRepo.IsOfficerByUserId(req.UserId, motion.ChapterId)) {
                AddError("UserId", I18nKey.Error.Motion.Vote.TargetNotOfficer);
                await SendErrorsAsync(400, ct);
                return;
            }

            // Caller must be an officer of the chapter or a parent chapter,
            // OR have the motions_vote_delegate permission
            var chapterAndAncestors = _chapterRepo.GetAncestorChain(motion.ChapterId)
                .Select(c => c.Id).ToList();
            var callerIsOfficer = _officerRepo.IsOfficerByUserIdForAnyChapter(_perms.UserId.Value, chapterAndAncestors);

            if (!callerIsOfficer &&
                !_perms.Has(motion.ChapterId, PermissionIdentifier.VoteDelegateMotions)) {
                AddError("UserId", I18nKey.Error.Motion.Vote.NoProxyPermission);
                await SendErrorsAsync(403, ct);
                return;
            }
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
