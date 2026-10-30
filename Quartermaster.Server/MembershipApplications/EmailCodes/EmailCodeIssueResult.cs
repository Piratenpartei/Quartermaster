namespace Quartermaster.Server.MembershipApplications.EmailCodes;

/// <summary>Code is the plain code to mail, or null when the per-address send limit is reached.</summary>
public record EmailCodeIssueResult(string? Code, bool LimitReached);
