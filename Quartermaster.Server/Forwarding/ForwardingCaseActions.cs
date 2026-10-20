using System;
using Quartermaster.Api.Forwarding;
using Quartermaster.Data.Forwarding;

namespace Quartermaster.Server.Forwarding;

/// <summary>The manual actions of the failure list. Neither talks to Redmine; the job picks retried steps up on its next run.</summary>
public class ForwardingCaseActions {
    private readonly ForwardingCaseRepository _cases;

    public ForwardingCaseActions(ForwardingCaseRepository cases) {
        _cases = cases;
    }

    /// <summary>Puts the failed steps of a failed case back into the queue with fresh attempts.</summary>
    public bool Retry(Guid caseId, DateTime nowUtc) {
        var forwardingCase = _cases.Get(caseId);
        if (forwardingCase == null || forwardingCase.Status != ForwardingCaseStatus.Failed)
            return false;

        foreach (var step in _cases.GetSteps(caseId)) {
            if (step.State != ForwardingStepState.Failed)
                continue;

            step.State = ForwardingStepState.Pending;
            step.Attempts = 0;
            step.NextAttemptAt = null;
            step.UpdatedAt = nowUtc;
            _cases.UpdateStep(step, ForwardingStepState.Failed);
        }

        return _cases.SetCaseStatus(caseId, ForwardingCaseStatus.Failed, ForwardingCaseStatus.Open, null);
    }

    /// <summary>Closes a case that someone handled by hand: every open step is skipped and the submitted data is deleted.</summary>
    public bool Resolve(Guid caseId, DateTime nowUtc) {
        var forwardingCase = _cases.Get(caseId);
        if (forwardingCase == null || forwardingCase.Status == ForwardingCaseStatus.Done || forwardingCase.Status == ForwardingCaseStatus.ManuallyResolved)
            return false;
        if (!_cases.SetCaseStatus(caseId, forwardingCase.Status, ForwardingCaseStatus.ManuallyResolved, nowUtc))
            return false;

        foreach (var step in _cases.GetSteps(caseId)) {
            if (step.State == ForwardingStepState.Done)
                continue;

            var readState = step.State;
            step.State = ForwardingStepState.Skipped;
            step.UpdatedAt = nowUtc;
            _cases.UpdateStep(step, readState);
        }

        _cases.ClearPayload(caseId);
        return true;
    }
}
