using System;
using System.Collections.Generic;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.Forwarding;

namespace Quartermaster.Server.Forwarding;

public record PlannedTicket(Guid TargetChapterId, ForwardingPurpose Purpose);

/// <param name="Tickets">Tickets in creation order; the federal ticket always comes first.</param>
/// <param name="MotionChapterId">Chapter that gets a review motion in Quartermaster, or null.</param>
/// <param name="StoreRecord">Whether the application or due selection is stored in Quartermaster.</param>
/// <param name="RecordStatus">Status of a stored due selection.</param>
/// <param name="RecordChapterId">Chapter of a stored record.</param>
/// <param name="DecidingChapterId">Chapter deciding on a reduction or handling an application, when one is determined.</param>
/// <param name="NoteAfterDecision">Whether the federal ticket gets the outcome of the Quartermaster vote as a note.</param>
public record ForwardingPlan(
    IReadOnlyList<PlannedTicket> Tickets,
    Guid? MotionChapterId,
    bool StoreRecord,
    DueSelectionStatus? RecordStatus,
    Guid? RecordChapterId,
    Guid? DecidingChapterId,
    bool NoteAfterDecision);
