using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Motions;
using Quartermaster.Data.ApplicantRegister;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.Forwarding;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Motions;
using Quartermaster.Server.Forwarding;
using Quartermaster.Server.Forwarding.SageEntry;
using Quartermaster.Server.MembershipApplications;
using Quartermaster.Server.PublicForms;

namespace Quartermaster.Server.ApplicantRegister;

public enum RegisterHitDismissal {
    AlreadyDismissed,

    /// <summary>A decision moved the application while the hit was being dismissed; nothing was changed.</summary>
    StatusChanged,

    Dismissed,

    /// <summary>The last hit lifted the federal step, so the chapter's approval became final.</summary>
    Approved
}

/// <summary>
/// The second approval under § 3 Abs. 2 BS: once the chapter approved an application that needs it, opens the federal motion and points the
/// application's forwarding cases at it, so the outcome note and the SAGE entry wait for the Bundesvorstand. An explicit rejection by either
/// level adds a register entry.
/// </summary>
public class FederalApprovalFlow {
    private readonly MembershipApplicationRepository _applications;
    private readonly MotionRepository _motions;
    private readonly ChapterRepository _chapters;
    private readonly ForwardingCaseRepository _cases;
    private readonly ApplicantRegisterRepository _register;
    private readonly ApplicationReviewService _review;
    private readonly ForwardingOutcomeHook _outcomeHook;
    private readonly SageEntryGate _gate;
    private readonly MembershipApplicationMailService _mailService;
    private readonly AuditLogRepository _auditLog;
    private readonly DueSelectionRepository _dueSelections;
    private readonly PublicFormSettings _settings;

    public FederalApprovalFlow(MembershipApplicationRepository applications, MotionRepository motions, ChapterRepository chapters,
        ForwardingCaseRepository cases, ApplicantRegisterRepository register, ApplicationReviewService review,
        ForwardingOutcomeHook outcomeHook, SageEntryGate gate, MembershipApplicationMailService mailService, AuditLogRepository auditLog,
        DueSelectionRepository dueSelections, PublicFormSettings settings) {
        _applications = applications;
        _motions = motions;
        _chapters = chapters;
        _cases = cases;
        _register = register;
        _review = review;
        _outcomeHook = outcomeHook;
        _gate = gate;
        _mailService = mailService;
        _auditLog = auditLog;
        _dueSelections = dueSelections;
        _settings = settings;
    }

    /// <summary>
    /// Dismisses one register hit as a different person. The last open hit lifts the federal approval unless the applicant declared a prior
    /// rejection; an application still before the chapter's decision loses the notice in its chapter motion, one already waiting for the federal
    /// step is approved as if the chapter's approval had been final. On a decided application only the hit is marked.
    /// </summary>
    public async Task<RegisterHitDismissal> DismissAsync(Guid applicationId, Guid hitId, Guid userId, CancellationToken ct) {
        var result = Dismiss(applicationId, hitId, userId);
        if (result != RegisterHitDismissal.Approved)
            return result;

        _outcomeHook.OnRecordDecided(ForwardingCaseKind.Application, applicationId, DateTime.UtcNow);
        var approved = _applications.Get(applicationId);
        if (approved != null && !_gate.HoldsApprovalMail(approved.Id))
            await _mailService.SendApplicationDecisionAsync(approved, ct);
        return result;
    }

    /// <summary>
    /// The hit, the lifted requirement and the approval commit together; mail and outcome note follow only after the commit. The application
    /// lock makes a concurrent dismissal of another hit wait, so the later one sees both and lifts.
    /// </summary>
    private RegisterHitDismissal Dismiss(Guid applicationId, Guid hitId, Guid userId) {
        using var tx = _cases.BeginTransaction();
        if (!_register.Dismiss(hitId, userId, DateTime.UtcNow))
            return RegisterHitDismissal.AlreadyDismissed;

        _applications.Lock(applicationId);
        var application = _applications.Get(applicationId);
        var result = LiftsFederalApproval(application) ? Lift(application!, userId) : RegisterHitDismissal.Dismissed;
        if (result == RegisterHitDismissal.StatusChanged)
            return result;

        tx.Commit();
        return result;
    }

    private bool LiftsFederalApproval(MembershipApplication? application) {
        if (application == null)
            return false;
        if (!application.Status.IsUndecided())
            return false;
        if (application.HasPriorDeclinedApplication)
            return false;
        return !_register.HasOpenHits(application.Id);
    }

    private RegisterHitDismissal Lift(MembershipApplication application, Guid userId) {
        if (application.Status == ApplicationStatus.PendingFederalApproval)
            return ApproveWithoutFederalStep(application, userId);

        if (!_applications.LiftFederalApproval(application.Id, application.Status))
            return RegisterHitDismissal.StatusChanged;
        if (application.Status == ApplicationStatus.Pending)
            _review.RemoveFederalApprovalNotice(application.Id);
        return RegisterHitDismissal.Dismissed;
    }

    private RegisterHitDismissal ApproveWithoutFederalStep(MembershipApplication application, Guid userId) {
        var federalMotion = application.FederalApprovalMotionId == null ? null : _motions.Get(application.FederalApprovalMotionId.Value);
        var chapterDecidedAt = federalMotion?.CreatedAt ?? DateTime.UtcNow;
        if (!_applications.ApproveOnChapterDecision(application.Id, userId, chapterDecidedAt))
            return RegisterHitDismissal.StatusChanged;

        CloseFederalMotion(federalMotion);
        PointCasesBackAtChapter(application);
        return RegisterHitDismissal.Approved;
    }

    private void CloseFederalMotion(Motion? federalMotion) {
        if (federalMotion == null)
            return;
        if (!_motions.UpdatePendingApprovalStatusInTransaction(federalMotion.Id, MotionApprovalStatus.ClosedWithoutAction))
            return;
        _auditLog.Log("Motion", federalMotion.Id, "ClosedWithoutAction", nameof(Motion.ApprovalStatus), null, "Register hits dismissed as another person");
    }

    private void PointCasesBackAtChapter(MembershipApplication application) {
        var chapterMotionId = _motions.ListByLinkedApplicationId(application.Id)
            .FirstOrDefault(m => m.Id != application.FederalApprovalMotionId)?.Id;
        foreach (var applicationCase in _cases.ListByRecord(ForwardingCaseKind.Application, application.Id)) {
            applicationCase.MotionId = chapterMotionId;
            applicationCase.DecidingChapterId = application.ChapterId;
            _cases.UpdateRouting(applicationCase);
        }
    }

    /// <summary>Runs before <see cref="ForwardingOutcomeHook.OnMotionResolved"/>, which would otherwise release the chapter motion's cases on the chapter's approval.</summary>
    public Task OnMotionResolvedAsync(Guid motionId) {
        var motion = _motions.Get(motionId);
        if (motion == null || motion.LinkedMembershipApplicationId == null)
            return Task.CompletedTask;
        var application = _applications.Get(motion.LinkedMembershipApplicationId.Value);
        if (application == null)
            return Task.CompletedTask;

        if (motion.ApprovalStatus == MotionApprovalStatus.Approved) {
            var decidedAtUtc = motion.ResolvedAt ?? DateTime.UtcNow;
            RequestFederalApproval(application, decidedAtUtc, () => VoteHearing(motion, decidedAtUtc));
        } else if (motion.ApprovalStatus == MotionApprovalStatus.Rejected && application.Status == ApplicationStatus.Rejected) {
            AddRejectedEntry(application, motion.ChapterId);
        }
        return Task.CompletedTask;
    }

    /// <summary>For an application decided in its processing dialog at <paramref name="decidedAtUtc"/>; runs before <see cref="ForwardingOutcomeHook.OnRecordDecided"/>.</summary>
    public Task OnRecordDecidedAsync(Guid applicationId, DateTime decidedAtUtc) {
        var application = _applications.Get(applicationId);
        if (application == null)
            return Task.CompletedTask;

        if (application.Status == ApplicationStatus.PendingFederalApproval) {
            RequestFederalApproval(application, decidedAtUtc, () => DialogHearing(application, decidedAtUtc));
        } else if (application.Status == ApplicationStatus.Rejected) {
            var decidingChapterId = DialogDecidingChapterId(application);
            if (decidingChapterId != null)
                AddRejectedEntry(application, decidingChapterId.Value);
        }
        return Task.CompletedTask;
    }

    private void RequestFederalApproval(MembershipApplication application, DateTime chapterDecidedAtUtc, Func<string> hearing) {
        if (application.Status != ApplicationStatus.PendingFederalApproval || application.FederalApprovalMotionId != null)
            return;
        var root = _chapters.GetRootChapter();
        if (root == null)
            return;

        var motionId = Guid.NewGuid();
        using var tx = _cases.BeginTransaction();
        if (!_applications.ClaimFederalApprovalMotion(application.Id, motionId))
            return;

        _review.CreateFederalApprovalMotion(application, motionId, root.Id, hearing(), chapterDecidedAtUtc, IsReduced(application));
        foreach (var applicationCase in _cases.ListByRecord(ForwardingCaseKind.Application, application.Id)) {
            applicationCase.MotionId = motionId;
            applicationCase.DecidingChapterId = root.Id;
            _cases.UpdateRouting(applicationCase);
        }
        tx.Commit();
    }

    private bool IsReduced(MembershipApplication application) {
        if (application.DueSelectionId == null)
            return false;
        var dueSelection = _dueSelections.Get(application.DueSelectionId.Value);
        return dueSelection != null && DueAmount.NeedsDecision(dueSelection.ToDto(), _settings.ReducedDueMaximum());
    }

    private void AddRejectedEntry(MembershipApplication application, Guid decidingChapterId) {
        if (_register.HasEntryFromApplication(application.Id))
            return;

        var nowUtc = DateTime.UtcNow;
        var today = DateTime.SpecifyKind(BerlinTime.FromUtc(nowUtc).Date, DateTimeKind.Utc);
        _register.Create(ApplicantRegisterEntry.FromRejectedApplication(application, decidingChapterId, today, nowUtc));
    }

    /// <summary>The federal motion exists once the chapter approved, so a rejection after it was decided at the federal level.</summary>
    private Guid? DialogDecidingChapterId(MembershipApplication application) {
        if (application.FederalApprovalMotionId != null)
            return _chapters.GetRootChapter()?.Id ?? application.ChapterId;
        return application.ChapterId ?? _chapters.GetRootChapter()?.Id;
    }

    private string VoteHearing(Motion motion, DateTime decidedAtUtc) {
        var votes = _motions.GetVotes(motion.Id);
        var approve = votes.Count(v => v.Vote == VoteType.Approve);
        var deny = votes.Count(v => v.Vote == VoteType.Deny);
        var abstain = votes.Count(v => v.Vote == VoteType.Abstain);
        return $"Gliederung {ChapterName(motion.ChapterId)} hat am {FormatDate(decidedAtUtc)} zugestimmt (Ja: {approve}, Nein: {deny}, Enthaltung: {abstain})";
    }

    private string DialogHearing(MembershipApplication application, DateTime decidedAtUtc) {
        var chapterName = application.ChapterId == null ? "" : ChapterName(application.ChapterId.Value);
        return $"Gliederung {chapterName} hat am {FormatDate(decidedAtUtc)} im Bearbeitungsdialog zugestimmt";
    }

    private string ChapterName(Guid chapterId) {
        return _chapters.Get(chapterId)?.Name ?? "";
    }

    private static string FormatDate(DateTime utc) {
        return BerlinTime.FromUtc(utc).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
    }
}
