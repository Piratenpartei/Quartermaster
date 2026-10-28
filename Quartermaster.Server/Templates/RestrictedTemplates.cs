using Quartermaster.Api;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Templates;

/// <summary>Templates that only callers with a specific global permission may see or touch.</summary>
public static class RestrictedTemplates {
    private const string ApplicantRegisterHitPrefix = "notifications.applicant_register_hit.";

    public static bool IsVisible(string? identifier, PermissionContext perms) {
        if (identifier == null)
            return true;
        if (!identifier.StartsWith(ApplicantRegisterHitPrefix))
            return true;
        return perms.HasGlobal(PermissionIdentifier.ManageApplicantRegister);
    }
}
