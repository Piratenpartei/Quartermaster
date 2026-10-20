namespace Quartermaster.Api.DueSelector;

public enum DueSelectionStatus {
    Pending,
    Approved,
    Rejected,
    AutoApproved,

    /// <summary>Decided outside Quartermaster, in a chapter's or the federal Redmine.</summary>
    Forwarded
}
