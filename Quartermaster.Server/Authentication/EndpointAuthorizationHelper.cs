using Quartermaster.Data.UserGlobalPermissions;
using System;
using System.Linq;
using System.Security.Claims;

namespace Quartermaster.Server.Authentication;

public static class EndpointAuthorizationHelper {
    public static Guid? GetUserId(ClaimsPrincipal user) {
        var claim = user.FindFirst(ClaimTypes.NameIdentifier);
        if (claim != null && Guid.TryParse(claim.Value, out var id))
            return id;
        return null;
    }

    public static bool HasGlobalPermission(Guid userId, string permission, UserGlobalPermissionRepository repo) {
        return repo.GetForUser(userId).Any(p => p.Identifier == permission);
    }
}
