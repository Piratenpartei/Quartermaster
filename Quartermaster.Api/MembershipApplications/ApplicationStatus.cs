namespace Quartermaster.Api.MembershipApplications;

public enum ApplicationStatus {
    Pending,
    Approved,
    Rejected,

    /// <summary>
    /// Confirmed application that arrived without an administrative division / chapter (manual or
    /// foreign address). Held here — no review motion, no officer notification — until someone with
    /// <c>applications_link_division</c> assigns a chapter, after which it moves to <see cref="Pending"/>.
    /// Appended last to preserve the persisted integer ordering.
    /// </summary>
    PendingDivisionLinking
}
