using System;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Members;

namespace Quartermaster.Server.AssemblyMotions;

public class AssemblyMotionUpvoteService {
    private readonly AssemblyMotionRepository _motions;
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly AssemblyMotionUpvoteRepository _upvotes;

    public AssemblyMotionUpvoteService(AssemblyMotionRepository motions, AssemblyMotionPortalRepository portals,
        AssemblyMotionUpvoteRepository upvotes) {
        _motions = motions;
        _portals = portals;
        _upvotes = upvotes;
    }

    public static bool IsWindowOpen(AssemblyMotionPortal portal, DateTime nowUtc) {
        if (!portal.PrioritizationEnabled)
            return false;
        if (portal.PrioritizationStartsAt == null || portal.PrioritizationEndsAt == null)
            return false;
        return portal.PrioritizationStartsAt <= nowUtc && nowUtc <= portal.PrioritizationEndsAt;
    }

    public static bool IsRankingPublic(AssemblyMotionPortal portal, DateTime nowUtc) {
        if (!portal.PrioritizationEnabled || portal.PrioritizationEndsAt == null)
            return false;
        return nowUtc > portal.PrioritizationEndsAt;
    }

    public static bool IsCountPublic(AssemblyMotionPortal portal, AssemblyMotion motion, DateTime nowUtc)
        => IsRankingPublic(portal, nowUtc) && CanReceiveUpvotes(motion);

    public static bool CanUpvote(AssemblyMotionPortal portal, AssemblyMotion motion, bool isEligible, DateTime nowUtc) {
        if (!isEligible)
            return false;
        if (!IsWindowOpen(portal, nowUtc))
            return false;
        return CanReceiveUpvotes(motion);
    }

    public static bool CanReceiveUpvotes(AssemblyMotion motion)
        => motion.Status == AssemblyMotionStatus.Submitted || motion.Status == AssemblyMotionStatus.Admitted;

    public AssemblyMotionOutcome Upvote(Guid motionId, Member member, DateTime nowUtc) {
        using var tx = _motions.BeginTransaction();

        var motion = _motions.GetForUpdate(motionId);
        if (motion == null)
            return AssemblyMotionOutcome.Fail(404, I18nKey.Error.AssemblyMotion.NotFound);

        var portal = _portals.Get(motion.PortalId)
            ?? throw new InvalidOperationException($"Portal {motion.PortalId} not found for motion {motion.Id}.");
        if (!IsWindowOpen(portal, nowUtc))
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.PrioritizationNotOpen);
        if (!CanReceiveUpvotes(motion))
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.InvalidState);

        // The motion row lock serializes concurrent calls, so the check below cannot race an insert.
        if (!_upvotes.Exists(motion.Id, member.Id)) {
            _upvotes.Insert(new AssemblyMotionUpvote {
                MotionId = motion.Id,
                MemberId = member.Id,
                CreatedAt = nowUtc
            });
        }

        tx.Commit();
        return AssemblyMotionOutcome.Ok(motion);
    }

    public AssemblyMotionOutcome Remove(Guid motionId, Member member, DateTime nowUtc) {
        using var tx = _motions.BeginTransaction();

        var motion = _motions.GetForUpdate(motionId);
        if (motion == null)
            return AssemblyMotionOutcome.Fail(404, I18nKey.Error.AssemblyMotion.NotFound);

        var portal = _portals.Get(motion.PortalId)
            ?? throw new InvalidOperationException($"Portal {motion.PortalId} not found for motion {motion.Id}.");
        if (!IsWindowOpen(portal, nowUtc))
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.PrioritizationNotOpen);

        _upvotes.Delete(motion.Id, member.Id);

        tx.Commit();
        return AssemblyMotionOutcome.Ok(motion);
    }
}
