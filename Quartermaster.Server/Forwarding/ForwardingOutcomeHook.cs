using System;
using System.Collections.Generic;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.Motions;
using Quartermaster.Data.Forwarding;
using Quartermaster.Data.Motions;

namespace Quartermaster.Server.Forwarding;

/// <summary>Releases the outcome note of forwarding cases once they are decided in Quartermaster, by their motion or directly on the record.</summary>
public class ForwardingOutcomeHook {
    private readonly ForwardingCaseRepository _cases;
    private readonly MotionRepository _motions;

    public ForwardingOutcomeHook(ForwardingCaseRepository cases, MotionRepository motions) {
        _cases = cases;
        _motions = motions;
    }

    /// <summary>For a due selection or application decided in its own processing dialog instead of through its motion.</summary>
    public void OnRecordDecided(ForwardingCaseKind kind, Guid recordId, DateTime nowUtc) {
        Release(_cases.ListByRecord(kind, recordId), nowUtc);
    }

    public void OnMotionResolved(Guid motionId, DateTime nowUtc) {
        var motion = _motions.Get(motionId);
        if (motion == null || motion.ApprovalStatus == MotionApprovalStatus.Pending)
            return;

        Release(_cases.ListByMotion(motionId), nowUtc);
    }

    private void Release(List<ForwardingCase> cases, DateTime nowUtc) {
        foreach (var forwardingCase in cases) {
            foreach (var step in _cases.GetSteps(forwardingCase.Id)) {
                if (step.Kind != ForwardingStepKind.AddOutcomeNote || step.State != ForwardingStepState.Waiting)
                    continue;

                step.State = ForwardingStepState.Pending;
                step.NextAttemptAt = null;
                step.UpdatedAt = nowUtc;
                _cases.UpdateStep(step, ForwardingStepState.Waiting);
            }
        }
    }
}
