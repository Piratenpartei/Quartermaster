using System;
using System.Collections.Generic;
using InterpolatedSql.Dapper;
using Quartermaster.Data.Abstract;
using Quartermaster.Data.Permissions;

namespace Quartermaster.Data.UserGlobalPermissions;

public class UserGlobalPermissionRepository : RepositoryBase<Permission> {
    private readonly SqlContext _context;

    internal UserGlobalPermissionRepository(SqlContext context) {
        _context = context;
    }

    public IEnumerable<Permission> GetForUser(Guid userId) {
        using var con = _context.GetConnection();
        return con.SqlBuilder($"SELECT * FROM UserGlobalPermissions WHERE UserId = {userId}")
            .Query<Permission>();
    }

    public void AddForUser(Guid userId, Permission permission) {
        ThrowOnEmptyGuid(permission, p => p.Id);

        using var con = _context.GetConnection();
        con.SqlBuilder($"INSERT IGNORE INTO UserGlobalPermissions (UserId, PermissionId) " +
            $"VALUES ({userId}, {permission.Id})").Execute();
    }

    public void RemoveForUser(Guid userId, Permission permission) {
        ThrowOnEmptyGuid(permission, p => p.Id);

        using var con = _context.GetConnection();
        con.SqlBuilder($"DELETE FROM UserGlobalPermissions WHERE UserId = {userId} AND " +
            $"PermissionId = {permission.Id}").Execute();
    }
}