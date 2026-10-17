using Quartermaster.Api;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.AssemblyAssistant.Endpoints;

/// <summary>Shared check for the setup endpoints. Returns the HTTP status to send, or null when the request may proceed.</summary>
public static class AssemblySetupGuard {
    public static int? Check(PermissionContext perms) {
        if (perms.UserId == null)
            return 401;
        if (!perms.HasGlobal(PermissionIdentifier.EditOptions))
            return 403;
        return null;
    }
}
