namespace Quartermaster.Api.MembershipApplications;

public static class ApplicationStatusExtensions {
    /// <summary>True while neither the chapter nor the Bundesvorstand has decided the application for good.</summary>
    public static bool IsUndecided(this ApplicationStatus status) {
        return status == ApplicationStatus.Pending
            || status == ApplicationStatus.PendingDivisionLinking
            || status == ApplicationStatus.PendingFederalApproval;
    }
}
