using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Data;

namespace Quartermaster.Server.Notifications;

/// <summary>
/// Notifies exactly the users for whom <c>PermissionContext.HasGlobal(ManageApplicantRegister)</c> holds:
/// direct global grants and global role assignments. Chapter grants never count, so nobody receives hits
/// without also seeing the trigger in their notification settings.
/// </summary>
public class ApplicantRegisterHitRecipientResolver : IRecipientResolver {
    private readonly DbContext _db;

    public ApplicantRegisterHitRecipientResolver(DbContext db) {
        _db = db;
    }

    public string TriggerId => NotificationTriggers.ApplicantRegisterHit;

    public IReadOnlyList<NotificationRecipient> Resolve(object payload) {
        if (payload is not ApplicantRegisterHitPayload)
            return Array.Empty<NotificationRecipient>();

        var permissionId = _db.Permissions
            .Where(perm => perm.Identifier == PermissionIdentifier.ManageApplicantRegister)
            .Select(perm => (Guid?)perm.Id)
            .FirstOrDefault();

        var directUserIds = permissionId.HasValue
            ? _db.UserGlobalPermissions
                .Where(ugp => ugp.PermissionId == permissionId.Value)
                .Select(ugp => ugp.UserId)
                .ToList()
            : new List<Guid>();

        var roleUserIds = _db.UserRoleAssignments
            .Join(_db.RolePermissions, a => a.RoleId, rp => rp.RoleId, (a, rp) => new { a, rp })
            .Where(x => x.a.ChapterId == null && x.rp.PermissionIdentifier == PermissionIdentifier.ManageApplicantRegister)
            .Select(x => x.a.UserId)
            .ToList();

        var userIds = directUserIds
            .Union(roleUserIds)
            .ToList();
        if (userIds.Count == 0)
            return Array.Empty<NotificationRecipient>();

        return _db.Users
            .Where(u => userIds.Contains(u.Id) && u.DeletedAt == null)
            .Select(u => new { u.Id, u.Email })
            .ToList()
            .Select(u => new NotificationRecipient(u.Id, u.Email ?? ""))
            .ToList();
    }
}
