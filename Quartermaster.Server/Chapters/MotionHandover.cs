using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.Motions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.Forwarding;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Motions;
using Quartermaster.Data.Options;
using Quartermaster.Server.MembershipApplications;
using Quartermaster.Server.Motions;

namespace Quartermaster.Server.Chapters;

/// <summary>
/// Moves the waiting motions of a marked chapter to the chapter that now decides, by closing each without action and creating a fresh copy there, so
/// votes start over. The linked application or due selection and its forwarding cases follow the new motion; nothing is sent to a Redmine.
/// </summary>
public class MotionHandover {
    private readonly ChapterRepository _chapters;
    private readonly MotionRepository _motions;
    private readonly MembershipApplicationRepository _applications;
    private readonly DueSelectionRepository _dueSelections;
    private readonly ForwardingCaseRepository _cases;
    private readonly ForwardingSettingsRepository _settings;
    private readonly OptionRepository _options;
    private readonly ApplicationReduction _reduction;
    private readonly ApplicationReviewService _review;
    private readonly MotionSubmittedNotifier _motionNotifier;
    private readonly List<Action> _pendingNotifications = [];

    public MotionHandover(ChapterRepository chapters, MotionRepository motions, MembershipApplicationRepository applications,
        DueSelectionRepository dueSelections, ForwardingCaseRepository cases, ForwardingSettingsRepository settings, OptionRepository options,
        ApplicationReduction reduction, ApplicationReviewService review, MotionSubmittedNotifier motionNotifier) {
        _chapters = chapters;
        _motions = motions;
        _applications = applications;
        _dueSelections = dueSelections;
        _cases = cases;
        _settings = settings;
        _options = options;
        _reduction = reduction;
        _review = review;
        _motionNotifier = motionNotifier;
    }

    /// <summary>Where the waiting motions would go if the chapter had these marks, grouped by target; writes nothing.</summary>
    public List<HandoverPreview> Preview(Guid chapterId, bool noMemberAdministration, bool unableToAct) {
        var chain = _chapters.GetAncestorChain(chapterId);
        if (chain.Count == 0)
            return [];

        chain[0].NoMemberAdministration = noMemberAdministration;
        chain[0].UnableToAct = unableToAct;
        var names = chain.ToDictionary(c => c.Id, c => c.Name);
        return Plan(chain)
            .GroupBy(h => h.TargetChapterId)
            .Select(g => new HandoverPreview(g.Key, names[g.Key], g.Count()))
            .ToList();
    }

    /// <summary>
    /// Runs in the caller's transaction, after the chapter's marks are stored. Returns the number of motions handed over; a motion decided
    /// meanwhile is skipped. The notifications for the target chapters wait for <see cref="SendNotifications"/> after the commit.
    /// </summary>
    public int HandOver(Guid chapterId) {
        var chain = _chapters.GetAncestorChain(chapterId);
        var count = 0;
        foreach (var handover in Plan(chain)) {
            if (HandOver(handover, chain))
                count++;
        }
        return count;
    }

    public void SendNotifications() {
        foreach (var notify in _pendingNotifications)
            notify();
        _pendingNotifications.Clear();
    }

    private List<PlannedHandover> Plan(List<Chapter> chain) {
        if (chain.Count < 2)
            return [];

        var marked = chain[0];
        if (marked.MarkFor(ChapterMarkPurpose.MemberAdministration) == null)
            return [];

        var forwardingEnabled = _options.IsGlobalEnabled(ForwardingOptionKeys.MembershipApplicationEnabled);
        return _motions.ListPendingInChapter(marked.Id)
            .Where(m => marked.UnableToAct || IsLinked(m))
            .Select(m => new PlannedHandover(m, TargetFor(m, chain, forwardingEnabled)))
            .ToList();
    }

    private static bool IsLinked(Motion motion) {
        return motion.LinkedMembershipApplicationId != null || motion.LinkedDueSelectionId != null;
    }

    private Guid TargetFor(Motion motion, List<Chapter> chain, bool forwardingEnabled) {
        if (motion.LinkedMembershipApplicationId != null && forwardingEnabled)
            return ApplicationHandlingChapter(chain);

        var purpose = IsLinked(motion) ? ChapterMarkPurpose.MemberAdministration : ChapterMarkPurpose.Motion;
        return ChapterMarkRouting.Resolve(chain, purpose).DecidingChapterId;
    }

    /// <summary>A handover never creates tickets, so a chapter handling applications through a Redmine is passed over as well.</summary>
    private Guid ApplicationHandlingChapter(List<Chapter> chain) {
        for (var i = 1; i < chain.Count - 1; i++) {
            if (HandlesApplicationsInQuartermaster(chain[i]))
                return chain[i].Id;
        }
        return chain[^1].Id;
    }

    private bool HandlesApplicationsInQuartermaster(Chapter chapter) {
        if (chapter.MarkFor(ChapterMarkPurpose.MemberAdministration) != null)
            return false;
        return _settings.GetChapter(chapter.Id)?.ApplicationMode == ForwardingMode.Quartermaster;
    }

    private bool HandOver(PlannedHandover handover, List<Chapter> chain) {
        var old = handover.Motion;
        if (!_motions.UpdatePendingApprovalStatusInTransaction(old.Id, MotionApprovalStatus.ClosedWithoutAction))
            return false;

        var replacement = Motion.Create(handover.TargetChapterId, old.AuthorName, old.AuthorEmail, old.Title, old.TextMarkdown, old.Text,
            DateTime.UtcNow, old.LinkedMembershipApplicationId, old.LinkedDueSelectionId, OnBehalfOf(old, chain[0]));
        replacement.IsPublic = old.IsPublic;
        _motions.Create(replacement);
        _motions.SetHandedOverTo(old.Id, replacement.Id);

        foreach (var forwardingCase in _cases.ListByMotion(old.Id))
            _cases.PointAtMotion(forwardingCase.Id, replacement.Id, handover.TargetChapterId);

        MoveLinkedRecord(replacement, chain.First(c => c.Id == handover.TargetChapterId));
        return true;
    }

    /// <summary>A motion handed over before keeps naming the chapter it was originally meant for.</summary>
    private static ChapterMarkSkip OnBehalfOf(Motion motion, Chapter marked) {
        var mark = marked.MarkFor(ChapterMarkPurpose.MemberAdministration)!.Value;
        if (motion.OnBehalfOfChapterId != null)
            return new ChapterMarkSkip(motion.OnBehalfOfChapterId.Value, motion.OnBehalfOfReason ?? mark);
        return new ChapterMarkSkip(marked.Id, mark);
    }

    private void MoveLinkedRecord(Motion replacement, Chapter target) {
        if (replacement.LinkedMembershipApplicationId != null) {
            MoveApplication(replacement.LinkedMembershipApplicationId.Value, target.Id);
        } else if (replacement.LinkedDueSelectionId != null) {
            MoveDueSelection(replacement.LinkedDueSelectionId.Value, target.Id);
        } else {
            _pendingNotifications.Add(() => _motionNotifier.Notify(replacement, target));
        }
    }

    private void MoveApplication(Guid applicationId, Guid targetChapterId) {
        var application = _applications.Get(applicationId);
        if (application == null)
            return;

        _applications.SetChapter(applicationId, targetChapterId);
        application.ChapterId = targetChapterId;
        var isReduced = _reduction.IsReduced(application);
        _pendingNotifications.Add(() => _review.NotifyApplicationSubmitted(application, targetChapterId, isReduced));
    }

    private void MoveDueSelection(Guid dueSelectionId, Guid targetChapterId) {
        var record = _dueSelections.Get(dueSelectionId);
        if (record == null)
            return;

        _dueSelections.SetDecidingChapter(dueSelectionId, targetChapterId, record.Status, record.ProcessedAt);
        record.ChapterId = targetChapterId;
        _pendingNotifications.Add(() => _review.NotifyDueSelectionSubmitted(record));
    }

    private record PlannedHandover(Motion Motion, Guid TargetChapterId);
}
