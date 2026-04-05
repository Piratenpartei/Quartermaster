using System.Collections.Generic;
using System.Linq;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.Roles;

namespace Quartermaster.Server.Roles;

public static class RolePermissionScopeValidator {
    /// <summary>
    /// Returns null if all identifiers exist and match the given scope, otherwise an error message.
    /// </summary>
    public static string? Validate(List<string> identifiers, RoleScope scope, PermissionRepository permissionRepo) {
        var allPermissions = permissionRepo.GetAll().ToDictionary(p => p.Identifier, p => p);
        foreach (var id in identifiers) {
            if (!allPermissions.TryGetValue(id, out var perm))
                return $"Unbekannte Berechtigung: {id}";
            var matches = scope == RoleScope.Global ? perm.Global : !perm.Global;
            if (!matches)
                return $"Berechtigung '{id}' passt nicht zum Scope der Rolle.";
        }
        return null;
    }
}
