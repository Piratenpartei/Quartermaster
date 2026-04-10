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
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Motions;

public class MotionVoteEndpoint : Endpoint<MotionVoteRequest> {
    private readonly MotionRepository _motionRepo;
    private readonly ChapterOfficerRepository _officerRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly ChapterRepository _chapterRepo;

    public MotionVoteEndpoint(MotionRepository motionRepo, ChapterOfficerRepository officerRepo,
        UserChapterPermissionRepository chapterPermRepo, UserGlobalPermissionRepository globalPermRepo,
        ChapterRepository chapterRepo) {
        _motionRepo = motionRepo;
        _officerRepo = officerRepo;
        _chapterPermRepo = chapterPermRepo;
        _globalPermRepo = globalPermRepo;
        _chapterRepo = chapterRepo;
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

        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        if (!EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, PermissionIdentifier.SystemVote, _globalPermRepo) &&
            !EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, PermissionIdentifier.VoteMotions, _globalPermRepo) &&
            !_chapterPermRepo.HasPermissionForChapter(userId.Value, motion.ChapterId, PermissionIdentifier.VoteMotions)) {
            await SendForbiddenAsync(ct);
            return;
        }

        // Delegation: voting on behalf of another user requires additional checks.
        // system_vote holders can vote for anyone without delegation checks.
        var hasSystemVote = EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, PermissionIdentifier.SystemVote, _globalPermRepo);
        if (req.UserId != userId.Value && !hasSystemVote) {
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
            var callerIsOfficer = _officerRepo.IsOfficerByUserIdForAnyChapter(userId.Value, chapterAndAncestors);

            if (!callerIsOfficer &&
                !EndpointAuthorizationHelper.HasPermission(userId.Value, motion.ChapterId, PermissionIdentifier.VoteDelegateMotions, _globalPermRepo, _chapterPermRepo, _chapterRepo)) {
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
