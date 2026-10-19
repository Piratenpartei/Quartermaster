using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Accreditation;

/// <summary>Request checks shared by the endpoints that talk to the Keycloak admin API. Returns the HTTP status to send, or null when the request may proceed.</summary>
public static class KeycloakGuard {
    public static int? Check(PermissionContext perms, KeycloakSettings settings, string globalPermission) {
        if (perms.UserId == null)
            return 401;
        if (!perms.HasGlobal(globalPermission))
            return 403;
        if (!settings.IsConfigured)
            return 503;
        return null;
    }
}
