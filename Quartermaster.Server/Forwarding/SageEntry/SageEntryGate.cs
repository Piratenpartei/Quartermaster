using System;
using System.Linq;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.Forwarding;
using Quartermaster.Data.MembershipApplications;

namespace Quartermaster.Server.Forwarding.SageEntry;

/// <summary>Tells whether an application's approval mail waits for its SAGE entry and how its dues reduction stands.</summary>
public class SageEntryGate {
    private readonly ForwardingCaseRepository _cases;
    private readonly MembershipApplicationRepository _applications;
    private readonly DueSelectionRepository _dueSelections;

    public SageEntryGate(ForwardingCaseRepository cases, MembershipApplicationRepository applications, DueSelectionRepository dueSelections) {
        _cases = cases;
        _applications = applications;
        _dueSelections = dueSelections;
    }

    public bool HoldsApprovalMail(Guid applicationId) {
        var application = _applications.Get(applicationId);
        if (application == null || application.Status != ApplicationStatus.Approved)
            return false;

        var applicationCases = _cases.ListByRecord(ForwardingCaseKind.Application, applicationId);
        return applicationCases.Any(c => _cases.GetSteps(c.Id).Any(s => s.Kind == ForwardingStepKind.AwaitSageEntry));
    }

    public ApprovalMailStatus StatusOf(Guid applicationId) {
        var none = new ApprovalMailStatus(ApprovalMailState.None, null, null);
        var application = _applications.Get(applicationId);
        if (application == null || application.Status != ApplicationStatus.Approved)
            return none;

        var sageCase = _cases.ListByRecord(ForwardingCaseKind.Application, applicationId)
            .Select(c => (Case: c, Steps: _cases.GetSteps(c.Id)))
            .FirstOrDefault(c => c.Steps.Any(step => step.Kind == ForwardingStepKind.AwaitSageEntry));
        if (sageCase.Steps == null)
            return none;

        var steps = sageCase.Steps;
        var awaitStep = steps.First(s => s.Kind == ForwardingStepKind.AwaitSageEntry);
        var ticketUrl = steps
            .OrderBy(s => s.Order)
            .FirstOrDefault(s => s.Kind == ForwardingStepKind.CreateTicket && s.State == ForwardingStepState.Done && s.TargetChapterId == awaitStep.TargetChapterId)
            ?.TicketUrl;
        if (awaitStep.State == ForwardingStepState.Done)
            return new ApprovalMailStatus(ApprovalMailState.Sent, awaitStep.UpdatedAt, ticketUrl);
        if (awaitStep.State == ForwardingStepState.Skipped) {
            return sageCase.Case.Status == ForwardingCaseStatus.ManuallyResolved
                ? new ApprovalMailStatus(ApprovalMailState.ManuallyResolved, null, ticketUrl)
                : none;
        }
        if (sageCase.Case.Status == ForwardingCaseStatus.Failed)
            return new ApprovalMailStatus(ApprovalMailState.Failed, null, ticketUrl);

        var noteDone = steps.Any(s => s.Kind == ForwardingStepKind.AddOutcomeNote && s.State == ForwardingStepState.Done);
        var waitingForReduction = !noteDone && ReductionOf(application) == ReductionOutcome.Pending;
        return new ApprovalMailStatus(waitingForReduction ? ApprovalMailState.WaitingForReduction : ApprovalMailState.WaitingForSage, null, ticketUrl);
    }

    public ReductionOutcome ReductionOf(MembershipApplication application) {
        if (application.DueSelectionId == null)
            return ReductionOutcome.None;

        var dueRecord = _dueSelections.Get(application.DueSelectionId.Value);
        if (dueRecord == null || dueRecord.SelectedValuation != SelectedValuation.Reduced)
            return ReductionOutcome.None;

        return dueRecord.Status switch {
            DueSelectionStatus.Pending => ReductionOutcome.Pending,
            DueSelectionStatus.Approved => ReductionOutcome.Approved,
            DueSelectionStatus.Rejected => ReductionOutcome.Rejected,
            DueSelectionStatus.Forwarded => ReductionOutcome.Outside,
            _ => ReductionOutcome.None
        };
    }
}
