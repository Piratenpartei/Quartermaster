using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Data;
using Quartermaster.Data.Chapters;

namespace Quartermaster.Server.Notifications;

/// <summary>
/// Phase-1 resolver: notify users holding <see cref="PermissionIdentifier.EditMotions"/> on
/// the motion's chapter — direct grants, role-derived (chapter or global), and role-derived
/// from ancestor chapters where the role inherits to children.
/// </summary>
public class MotionSubmittedRecipientResolver : IRecipientResolver {
    private readonly DbContext _db;
    private readonly ChapterRepository _chapterRepo;

    public MotionSubmittedRecipientResolver(DbContext db, ChapterRepository chapterRepo) {
        _db = db;
        _chapterRepo = chapterRepo;
    }

    public string TriggerId => NotificationTriggers.MotionSubmitted;

    public IReadOnlyList<NotificationRecipient> Resolve(object payload) {
        if (payload is not MotionSubmittedPayload p)
            return Array.Empty<NotificationRecipient>();

        var ancestorIds = _chapterRepo.GetAncestorChainIds(p.ChapterId).ToList();

        var permissionId = _db.Permissions
            .Where(perm => perm.Identifier == PermissionIdentifier.EditMotions)
            .Select(perm => (Guid?)perm.Id)
            .FirstOrDefault();

        var globalGrantedUserIds = permissionId.HasValue
            ? _db.UserGlobalPermissions
                .Where(ugp => ugp.PermissionId == permissionId.Value)
                .Select(ugp => ugp.UserId)
                .Distinct()
                .ToList()
            : new List<Guid>();

        var directGrantedUserIds = permissionId.HasValue
            ? _db.UserChapterPermissions
                .Where(ucp => ucp.PermissionId == permissionId.Value
                    && (ucp.ChapterId == p.ChapterId || ancestorIds.Contains(ucp.ChapterId)))
                .Select(ucp => ucp.UserId)
                .Distinct()
                .ToList()
            : new List<Guid>();

        var roleGrantedUserIds = _db.UserRoleAssignments
            .Join(_db.Roles, a => a.RoleId, r => r.Id, (a, r) => new { a, r })
            .Join(_db.RolePermissions, x => x.a.RoleId, rp => rp.RoleId, (x, rp) => new { x.a, x.r, rp })
            .Where(x => x.rp.PermissionIdentifier == PermissionIdentifier.EditMotions
                && (x.a.ChapterId == null
                    || x.a.ChapterId == p.ChapterId
                    || (ancestorIds.Contains(x.a.ChapterId.Value) && x.r.InheritsToChildren)))
            .Select(x => x.a.UserId)
            .Distinct()
            .ToList();

        var userIds = globalGrantedUserIds
            .Union(directGrantedUserIds)
            .Union(roleGrantedUserIds)
            .ToList();
        if (userIds.Count == 0)
            return Array.Empty<NotificationRecipient>();

        return _db.Users
            .Where(u => userIds.Contains(u.Id)
                && u.DeletedAt == null
                && u.Email != null
                && u.Email != "")
            .Select(u => new { u.Id, u.Email })
            .ToList()
            .Select(u => new NotificationRecipient(u.Id, u.Email))
            .ToList();
    }
}
