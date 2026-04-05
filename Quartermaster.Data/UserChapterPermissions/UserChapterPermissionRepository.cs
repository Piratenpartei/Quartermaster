using InterpolatedSql.Dapper;
using LinqToDB;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Roles;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.UserChapterPermissions;

public class UserChapterPermissionRepository {
    private readonly DbContext _context;
    private readonly RoleRepository _roleRepo;

    public UserChapterPermissionRepository(DbContext context, RoleRepository roleRepo) {
        _context = context;
        _roleRepo = roleRepo;
    }

    public bool HasPermissionForChapter(Guid userId, Guid chapterId, string identifier) {
        // Direct grant
        var permission = _context.Permissions.Where(p => p.Identifier == identifier).FirstOrDefault();
        if (permission != null) {
            var direct = _context.UserChapterPermissions
                .Where(ucp => ucp.UserId == userId && ucp.ChapterId == chapterId && ucp.PermissionId == permission.Id)
                .FirstOrDefault();
            if (direct != null)
                return true;
        }

        // Role-derived grant (chapter-scoped or global role applying to this chapter)
        return _roleRepo.GetChapterPermissionsViaRoles(userId, chapterId).Contains(identifier);
    }

    public bool HasPermissionWithInheritance(
        Guid userId, Guid chapterId, string identifier, ChapterRepository chapterRepo) {
        if (IsViewPermission(identifier)) {
            var ancestors = chapterRepo.GetAncestorChain(chapterId);
            foreach (var ancestor in ancestors) {
                if (HasPermissionForChapter(userId, ancestor.Id, identifier))
                    return true;
            }
            return false;
        }

        return HasPermissionForChapter(userId, chapterId, identifier);
    }

    public Dictionary<Guid, List<string>> GetAllForUser(Guid userId) {
        // Direct chapter grants
        var directs = _context.UserChapterPermissions
            .Where(ucp => ucp.UserId == userId)
            .InnerJoin(_context.Permissions, (ucp, p) => ucp.PermissionId == p.Id, (ucp, p) => new {
                ucp.ChapterId,
                p.Identifier
            })
            .ToList();

        var merged = new Dictionary<Guid, HashSet<string>>();
        foreach (var r in directs) {
            if (!merged.TryGetValue(r.ChapterId, out var set)) {
                set = new HashSet<string>();
                merged[r.ChapterId] = set;
            }
            set.Add(r.Identifier);
        }

        // Role-derived chapter grants (chapter-scoped assignments only — global assignments
        // apply to all chapters but aren't chapter-specific, so they're handled via HasGlobalPermission)
        var chapterAssignments = _roleRepo.GetAssignmentsForUser(userId)
            .Where(a => a.ChapterId.HasValue)
            .ToList();

        if (chapterAssignments.Count > 0) {
            var roleIds = chapterAssignments.Select(a => a.RoleId).Distinct().ToList();
            var rolePerms = _context.RolePermissions
                .Where(rp => roleIds.Contains(rp.RoleId))
                .ToList()
                .GroupBy(rp => rp.RoleId)
                .ToDictionary(g => g.Key, g => g.Select(rp => rp.PermissionIdentifier).ToList());

            foreach (var a in chapterAssignments) {
                if (!rolePerms.TryGetValue(a.RoleId, out var perms))
                    continue;
                if (!merged.TryGetValue(a.ChapterId!.Value, out var set)) {
                    set = new HashSet<string>();
                    merged[a.ChapterId.Value] = set;
                }
                foreach (var p in perms)
                    set.Add(p);
            }
        }

        return merged.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToList());
    }

    public void AddForUser(Guid userId, Guid chapterId, Guid permissionId) {
        var exists = _context.UserChapterPermissions
            .Where(ucp => ucp.UserId == userId && ucp.ChapterId == chapterId && ucp.PermissionId == permissionId)
            .FirstOrDefault();
        if (exists != null)
            return;

        _context.Insert(new UserChapterPermission {
            UserId = userId,
            ChapterId = chapterId,
            PermissionId = permissionId
        });
    }

    public void RemoveForUser(Guid userId, Guid chapterId, Guid permissionId) {
        _context.UserChapterPermissions
            .Where(ucp => ucp.UserId == userId && ucp.ChapterId == chapterId && ucp.PermissionId == permissionId)
            .Delete();
    }

    private static bool IsViewPermission(string identifier) {
        return identifier.EndsWith("_view");
    }
}