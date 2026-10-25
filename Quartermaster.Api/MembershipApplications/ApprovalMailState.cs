namespace Quartermaster.Api.MembershipApplications;

public enum ApprovalMailState {
    None,
    WaitingForReduction,
    WaitingForSage,
    Sent,
    ManuallyResolved,
    Failed
}
