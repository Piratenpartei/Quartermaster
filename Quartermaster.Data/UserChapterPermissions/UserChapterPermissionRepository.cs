using InterpolatedSql.Dapper;
using LinqToDB;
using Quartermaster.Data.Chapters;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.UserChapterPermissions;

public class UserChapterPermissionRepository {
    private readonly DbContext _context;

    public UserChapterPermissionRepository(DbContext context) {
        _context = context;
    }

    public bool HasPermissionForChapter(Guid userId, Guid chapterId, string identifier) {
        var permission = _context.Permissions.Where(p => p.Identifier == identifier).FirstOrDefault();
        if (permission == null)
            return false;

        return _context.UserChapterPermissions
            .Where(ucp => ucp.UserId == userId && ucp.ChapterId == chapterId && ucp.PermissionId == permission.Id)
            .FirstOrDefault() != null;
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
        var results = _context.UserChapterPermissions
            .Where(ucp => ucp.UserId == userId)
            .InnerJoin(_context.Permissions, (ucp, p) => ucp.PermissionId == p.Id, (ucp, p) => new {
                ucp.ChapterId,
                p.Identifier
            })
            .ToList();

        return results
            .GroupBy(r => r.ChapterId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.Identifier).ToList());
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