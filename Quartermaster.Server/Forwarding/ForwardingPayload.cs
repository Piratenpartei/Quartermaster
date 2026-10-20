using System;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.MembershipApplications;

namespace Quartermaster.Server.Forwarding;

/// <summary>Snapshot of a submission, stored as <c>ForwardingCase.PayloadJson</c> until every ticket of the case exists.</summary>
/// <param name="SubmittedByName">Display name of the logged-in account that submitted a due selection.</param>
/// <param name="EnteredByHand">True when a due selection's person data was typed in because no member record is linked.</param>
public record ForwardingPayload(
    MembershipApplicationDTO? Application,
    DueSelectionDTO? DueSelection,
    Guid? SubmittedByUserId,
    string? SubmittedByName,
    bool EnteredByHand,
    DateTime SubmittedAt);
