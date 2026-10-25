namespace Quartermaster.Server.Forwarding.SageEntry;

/// <summary>How the dues reduction that belongs to an application stands.</summary>
public enum ReductionOutcome {
    None,
    Pending,
    Approved,
    Rejected,

    /// <summary>Decided outside Quartermaster, so its result is not known here.</summary>
    Outside
}
