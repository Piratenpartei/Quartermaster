using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Motions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Motions;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.Chapters;

/// <summary>Feeds <see cref="TakeOverSchedule"/> with the chapters above an application motion and decides whether the motion is still waiting.</summary>
public class TakeOverPlanner {
    private readonly ChapterRepository _chapters;
    private readonly ApplicationHandling _applicationHandling;
    private readonly OptionRepository _options;

    public TakeOverPlanner(ChapterRepository chapters, ApplicationHandling applicationHandling, OptionRepository options) {
        _chapters = chapters;
        _applicationHandling = applicationHandling;
        _options = options;
    }

    /// <summary>The chapters above the given one, nearest first, the root last.</summary>
    public List<TakeOverCandidate> CandidatesAbove(Guid chapterId) {
        var chain = _chapters.GetAncestorChain(chapterId);
        if (chain.Count < 2)
            return [];

        var forwardingEnabled = _options.IsGlobalEnabled(ForwardingOptionKeys.MembershipApplicationEnabled);
        return chain
            .Skip(1)
            .Select(c => new TakeOverCandidate(c.Id, c.Name, c.EffectiveTakeOverPeriodDays(), HandlesApplications(c, c == chain[^1], forwardingEnabled)))
            .ToList();
    }

    public TakeOverStep? DueFor(Motion motion, MembershipApplication application, DateTime nowUtc) {
        if (!IsWaiting(motion, application))
            return null;
        return TakeOverSchedule.Due(CandidatesAbove(motion.ChapterId), application.SubmittedAt, nowUtc);
    }

    public TakeOverStep? NextFor(Motion motion, MembershipApplication application, DateTime nowUtc) {
        if (!IsWaiting(motion, application))
            return null;
        return TakeOverSchedule.Next(CandidatesAbove(motion.ChapterId), application.SubmittedAt, nowUtc);
    }

    /// <summary>The root chapter a waiting reduced due selection can be taken over to, or null when the motion is not such a motion or already sits at the root.</summary>
    public Guid? TakeOverRootFor(Motion motion) {
        if (motion.ApprovalStatus != MotionApprovalStatus.Pending)
            return null;
        if (motion.HandedOverToMotionId != null)
            return null;
        if (motion.LinkedDueSelectionId == null)
            return null;

        var chain = _chapters.GetAncestorChainIds(motion.ChapterId);
        if (chain.Count < 2)
            return null;
        return chain[^1];
    }

    private static bool IsWaiting(Motion motion, MembershipApplication application) {
        if (motion.ApprovalStatus != MotionApprovalStatus.Pending)
            return false;
        if (motion.LinkedMembershipApplicationId != application.Id)
            return false;
        if (motion.HandedOverToMotionId != null)
            return false;
        return application.Status == ApplicationStatus.Pending;
    }

    private bool HandlesApplications(Chapter chapter, bool isRoot, bool forwardingEnabled) {
        if (isRoot)
            return true;
        if (chapter.MarkFor(ChapterMarkPurpose.MemberAdministration) != null)
            return false;
        if (!forwardingEnabled)
            return true;
        return _applicationHandling.HandlesApplicationsInQuartermaster(chapter);
    }
}
