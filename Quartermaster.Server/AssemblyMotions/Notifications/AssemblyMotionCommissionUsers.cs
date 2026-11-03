using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Data;

namespace Quartermaster.Server.AssemblyMotions.Notifications;

/// <summary>The users holding <see cref="PermissionIdentifier.ManageAssemblyMotions"/> on exactly the given chapter (the permission does not inherit to sub-chapters).</summary>
public static class AssemblyMotionCommissionUsers {
    public static List<Guid> UserIds(DbContext db, Guid chapterId) {
        var permissionId = db.Permissions
            .Where(perm => perm.Identifier == PermissionIdentifier.ManageAssemblyMotions)
            .Select(perm => (Guid?)perm.Id)
            .FirstOrDefault();

        var globalGrantedUserIds = permissionId.HasValue
            ? db.UserGlobalPermissions
                .Where(ugp => ugp.PermissionId == permissionId.Value)
                .Select(ugp => ugp.UserId)
                .Distinct()
                .ToList()
            : new List<Guid>();

        var directGrantedUserIds = permissionId.HasValue
            ? db.UserChapterPermissions
                .Where(ucp => ucp.PermissionId == permissionId.Value && ucp.ChapterId == chapterId)
                .Select(ucp => ucp.UserId)
                .Distinct()
                .ToList()
            : new List<Guid>();

        var roleGrantedUserIds = db.UserRoleAssignments
            .Join(db.RolePermissions, a => a.RoleId, rp => rp.RoleId, (a, rp) => new { a, rp })
            .Where(x => x.rp.PermissionIdentifier == PermissionIdentifier.ManageAssemblyMotions
                && (x.a.ChapterId == null || x.a.ChapterId == chapterId))
            .Select(x => x.a.UserId)
            .Distinct()
            .ToList();

        return globalGrantedUserIds
            .Union(directGrantedUserIds)
            .Union(roleGrantedUserIds)
            .ToList();
    }
}
