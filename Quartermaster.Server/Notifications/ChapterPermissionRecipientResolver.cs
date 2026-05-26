using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;
using Quartermaster.Data;
using Quartermaster.Data.Chapters;

namespace Quartermaster.Server.Notifications;

/// <summary>
/// Base resolver for "users holding permission X on chapter C". Picks up direct
/// chapter grants (own + inheriting ancestors), global grants, and role-derived
/// grants (chapter / global / inheriting-from-ancestor). Subclasses bind to one
/// trigger and one permission.
/// </summary>
public abstract class ChapterPermissionRecipientResolver<TPayload> : IRecipientResolver
    where TPayload : class {

    private readonly DbContext _db;
    private readonly ChapterRepository _chapterRepo;

    protected ChapterPermissionRecipientResolver(DbContext db, ChapterRepository chapterRepo) {
        _db = db;
        _chapterRepo = chapterRepo;
    }

    public abstract string TriggerId { get; }

    /// <summary>Permission identifier whose holders should be notified.</summary>
    protected abstract string PermissionIdentifier { get; }

    /// <summary>Returns the chapter id the trigger is scoped to.</summary>
    protected abstract Guid ChapterIdFor(TPayload payload);

    public IReadOnlyList<NotificationRecipient> Resolve(object payload) {
        if (payload is not TPayload typed)
            return Array.Empty<NotificationRecipient>();

        var chapterId = ChapterIdFor(typed);
        var ancestorIds = _chapterRepo.GetAncestorChainIds(chapterId).ToList();

        var permissionId = _db.Permissions
            .Where(perm => perm.Identifier == PermissionIdentifier)
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
                    && (ucp.ChapterId == chapterId || ancestorIds.Contains(ucp.ChapterId)))
                .Select(ucp => ucp.UserId)
                .Distinct()
                .ToList()
            : new List<Guid>();

        var roleGrantedUserIds = _db.UserRoleAssignments
            .Join(_db.Roles, a => a.RoleId, r => r.Id, (a, r) => new { a, r })
            .Join(_db.RolePermissions, x => x.a.RoleId, rp => rp.RoleId, (x, rp) => new { x.a, x.r, rp })
            .Where(x => x.rp.PermissionIdentifier == PermissionIdentifier
                && (x.a.ChapterId == null
                    || x.a.ChapterId == chapterId
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
