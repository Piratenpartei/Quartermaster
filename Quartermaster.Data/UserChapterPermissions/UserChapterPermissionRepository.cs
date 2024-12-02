using InterpolatedSql.Dapper;
using System;

namespace Quartermaster.Data.UserChapterPermissions;

public class UserChapterPermissionRepository {
    private readonly DbContext _context;

    public UserChapterPermissionRepository(DbContext context) {
        _context = context;
    }

    public bool HasPermissionForChapter(Guid userId, Guid chapterId, string identifier) {
        var permission = _context.Permissions.GetByIdentifier(identifier);
        if (permission == null)
            return false;

        using var con = _context.GetConnection();
        return con.SqlBuilder($"SELECT * FROM UserChapterPermissions WHERE UserId = {userId} AND " +
            $"ChapterId = {chapterId} AND PermissionId = {permission.Id}")
            .QuerySingle<UserChapterPermission>() != null;
    }
}