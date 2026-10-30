namespace Quartermaster.Server.MembershipApplications.EmailCodes;

public enum EmailCodeCheckResult {
    Valid,
    Wrong,
    Expired,
    Locked
}
