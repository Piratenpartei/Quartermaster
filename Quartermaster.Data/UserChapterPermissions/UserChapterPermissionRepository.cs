using InterpolatedSql.Dapper;
using System;
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
}