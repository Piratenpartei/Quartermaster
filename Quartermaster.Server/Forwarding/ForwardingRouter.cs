using System;
using System.Collections.Generic;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.Forwarding;

namespace Quartermaster.Server.Forwarding;

/// <summary>Decides where a submission goes. Pure: everything it needs comes in through <see cref="ForwardingChapterContext"/>.</summary>
public static class ForwardingRouter {
    public static ForwardingPlan PlanDueSelection(ForwardingChapterContext ctx, decimal amount, bool isReduced, bool forwardingEnabled,
        decimal regularMinimum, decimal stateDecisionMinimum, bool partOfApplication) {
        var needsDecision = isReduced && amount < regularMinimum;
        var forwards = forwardingEnabled && ctx.FederalId != null;

        if (!needsDecision) {
            if (!forwards)
                return Plan([], null, !partOfApplication, DueSelectionStatus.AutoApproved, ctx.StateId ?? ctx.FederalId, null, false);
            if (partOfApplication)
                return Plan([], null, false, null, null, null, false);
            return Plan([new PlannedTicket(ctx.FederalId!.Value, ForwardingPurpose.Regular)], null, false, null, null, null, false);
        }

        var decidesAtState = ctx.StateId != null && amount >= stateDecisionMinimum;
        var deciding = decidesAtState ? ctx.StateId : ctx.FederalId;
        if (!forwards)
            return Plan([], deciding, true, DueSelectionStatus.Pending, deciding, deciding, false);

        var federal = new PlannedTicket(ctx.FederalId!.Value, ForwardingPurpose.Reduction);
        var mode = decidesAtState ? ctx.StateReductionMode : ctx.FederalReductionMode;
        if (mode == ForwardingMode.Quartermaster)
            return Plan([federal], deciding, true, DueSelectionStatus.Pending, deciding, deciding, true);
        if (mode == ForwardingMode.Redmine && decidesAtState)
            return Plan([federal, new PlannedTicket(ctx.StateId!.Value, ForwardingPurpose.Reduction)], null, false, null, null, deciding, false);

        return Plan([federal], null, false, null, null, deciding, false);
    }

    public static ForwardingPlan PlanApplication(ForwardingChapterContext ctx, Guid? applicationChapterId, bool forwardingEnabled) {
        if (!forwardingEnabled || ctx.FederalId == null)
            return Plan([], applicationChapterId, true, null, applicationChapterId, applicationChapterId, false);

        var federalId = ctx.FederalId.Value;
        var federal = new PlannedTicket(federalId, ForwardingPurpose.Application);
        if (ctx.HandlingChapterId != null) {
            var handling = ctx.HandlingChapterId.Value;
            if (ctx.HandlingApplicationMode == ForwardingMode.Quartermaster)
                return Plan([federal], handling, true, null, handling, handling, true);
            return Plan([federal, new PlannedTicket(handling, ForwardingPurpose.Application)], null, false, null, null, handling, false);
        }

        // No chapter could be determined from the address: the application waits for someone to link its division, as without forwarding.
        if (applicationChapterId == null)
            return Plan([federal], null, true, null, null, null, false);

        var atFederal = applicationChapterId == federalId;
        if (atFederal && ctx.FederalApplicationMode == ForwardingMode.Quartermaster)
            return Plan([federal], federalId, true, null, federalId, federalId, true);

        return Plan([federal], null, false, null, null, atFederal ? federalId : null, false);
    }

    private static ForwardingPlan Plan(List<PlannedTicket> tickets, Guid? motionChapterId, bool storeRecord, DueSelectionStatus? recordStatus,
        Guid? recordChapterId, Guid? decidingChapterId, bool noteAfterDecision) {
        return new ForwardingPlan(tickets, motionChapterId, storeRecord, recordStatus, recordChapterId, decidingChapterId, noteAfterDecision);
    }
}
