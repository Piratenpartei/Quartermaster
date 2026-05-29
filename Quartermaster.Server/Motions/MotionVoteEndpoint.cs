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
using Quartermaster.Data.Members;
using Quartermaster.Data.Motions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Motions;

public class MotionVoteEndpoint : Endpoint<MotionVoteRequest> {
    private readonly MotionRepository _motionRepo;
    private readonly ChapterOfficerRepository _officerRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly MemberRepository _memberRepo;
    private readonly MotionResolutionDecisionMailer _decisionMailer;
    private readonly PermissionContext _perms;

    public MotionVoteEndpoint(MotionRepository motionRepo, ChapterOfficerRepository officerRepo,
        ChapterRepository chapterRepo, MemberRepository memberRepo,
        MotionResolutionDecisionMailer decisionMailer, PermissionContext perms) {
        _motionRepo = motionRepo;
        _officerRepo = officerRepo;
        _chapterRepo = chapterRepo;
        _memberRepo = memberRepo;
        _decisionMailer = decisionMailer;
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

        // The vote belongs to an officer (member) of the motion's chapter.
        if (!_officerRepo.IsOfficer(req.MemberId, motion.ChapterId)) {
            AddError("MemberId", I18nKey.Error.Motion.Vote.TargetNotOfficer);
            await SendErrorsAsync(400, ct);
            return;
        }

        // Recording a vote for an officer other than yourself needs delegation rights —
        // unless you hold system_vote. (Self = the recorder's own member.)
        var callerMember = _memberRepo.GetByUserId(_perms.UserId.Value);
        var isSelf = callerMember != null && callerMember.Id == req.MemberId;
        if (!isSelf && !_perms.HasGlobal(PermissionIdentifier.SystemVote)) {
            var chapterAndAncestors = _chapterRepo.GetAncestorChain(motion.ChapterId)
                .Select(c => c.Id).ToList();
            var callerIsOfficer = _officerRepo.IsOfficerByUserIdForAnyChapter(_perms.UserId.Value, chapterAndAncestors);
            if (!callerIsOfficer &&
                !_perms.Has(motion.ChapterId, PermissionIdentifier.VoteDelegateMotions)) {
                AddError("MemberId", I18nKey.Error.Motion.Vote.NoProxyPermission);
                await SendErrorsAsync(403, ct);
                return;
            }
        }

        _motionRepo.CastVote(new MotionVote {
            MotionId = req.MotionId,
            MemberId = req.MemberId,
            CastByUserId = _perms.UserId.Value,
            Vote = (VoteType)req.Vote,
            VotedAt = DateTime.UtcNow
        });

        if (_motionRepo.TryAutoResolve(req.MotionId, _officerRepo)) {
            await _decisionMailer.NotifyAsync(req.MotionId, ct);
        }

        await SendOkAsync(ct);
    }
}
