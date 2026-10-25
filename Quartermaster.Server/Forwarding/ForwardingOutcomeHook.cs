using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Motions;
using Quartermaster.Data.Forwarding;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Motions;

namespace Quartermaster.Server.Forwarding;

/// <summary>Releases the outcome note and the SAGE entry wait of forwarding cases once they are decided in Quartermaster, by their motion or directly on the record.</summary>
public class ForwardingOutcomeHook {
    private readonly ForwardingCaseRepository _cases;
    private readonly MotionRepository _motions;
    private readonly MembershipApplicationRepository _applications;

    public ForwardingOutcomeHook(ForwardingCaseRepository cases, MotionRepository motions, MembershipApplicationRepository applications) {
        _cases = cases;
        _motions = motions;
        _applications = applications;
    }

    /// <summary>For a due selection or application decided in its own processing dialog instead of through its motion.</summary>
    public void OnRecordDecided(ForwardingCaseKind kind, Guid recordId, DateTime nowUtc) {
        Release(_cases.ListByRecord(kind, recordId), kind == ForwardingCaseKind.DueSelection ? recordId : null, nowUtc);
    }

    public void OnMotionResolved(Guid motionId, DateTime nowUtc) {
        var motion = _motions.Get(motionId);
        if (motion == null || motion.ApprovalStatus == MotionApprovalStatus.Pending)
            return;

        var decidedCases = _cases.ListByMotion(motionId)
            .Where(c => Decides(motion, c))
            .ToList();
        Release(decidedCases, motion.LinkedDueSelectionId, nowUtc);
    }

    /// <summary>
    /// A motion on an application closed without action leaves the application undecided; it is decided later in its processing dialog, so the
    /// application case waits for that instead of taking "ohne Beschluss geschlossen" as its outcome.
    /// </summary>
    public static bool Decides(Motion motion, ForwardingCase forwardingCase) {
        if (motion.ApprovalStatus == MotionApprovalStatus.Pending)
            return false;
        return forwardingCase.Kind != ForwardingCaseKind.Application || motion.ApprovalStatus != MotionApprovalStatus.ClosedWithoutAction;
    }

    /// <summary>
    /// A decided reduction also frees the application it belongs to, once that is decided too. The application is found through the reduction's
    /// case and through the due record itself, because a reduction has no case of its own while due selection forwarding is off.
    /// </summary>
    private void Release(List<ForwardingCase> cases, Guid? decidedDueSelectionId, DateTime nowUtc) {
        var applicationIds = new HashSet<Guid>();
        foreach (var forwardingCase in cases) {
            ReleaseSteps(forwardingCase, nowUtc);
            if (forwardingCase.Kind == ForwardingCaseKind.DueSelection && forwardingCase.MembershipApplicationId != null)
                applicationIds.Add(forwardingCase.MembershipApplicationId.Value);
        }

        var reducedApplication = decidedDueSelectionId == null ? null : _applications.GetByDueSelectionId(decidedDueSelectionId.Value);
        if (reducedApplication != null)
            applicationIds.Add(reducedApplication.Id);

        foreach (var applicationId in applicationIds)
            ReleaseDecidedApplication(applicationId, nowUtc);
    }

    private void ReleaseDecidedApplication(Guid applicationId, DateTime nowUtc) {
        var application = _applications.Get(applicationId);
        if (application == null || (application.Status != ApplicationStatus.Approved && application.Status != ApplicationStatus.Rejected))
            return;

        foreach (var applicationCase in _cases.ListByRecord(ForwardingCaseKind.Application, applicationId))
            ReleaseSteps(applicationCase, nowUtc);
    }

    private void ReleaseSteps(ForwardingCase forwardingCase, DateTime nowUtc) {
        foreach (var step in _cases.GetSteps(forwardingCase.Id)) {
            if (!IsReleasable(step))
                continue;

            step.State = ForwardingStepState.Pending;
            step.NextAttemptAt = null;
            step.UpdatedAt = nowUtc;
            _cases.UpdateStep(step, ForwardingStepState.Waiting);
        }
    }

    private static bool IsReleasable(ForwardingStep step) {
        if (step.State != ForwardingStepState.Waiting)
            return false;
        return step.Kind == ForwardingStepKind.AddOutcomeNote || step.Kind == ForwardingStepKind.AwaitSageEntry;
    }
}
