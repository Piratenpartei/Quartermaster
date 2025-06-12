using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;
using Quartermaster.Data.Permissions;

namespace Quartermaster.Data.UserGlobalPermissions;

public class UserGlobalPermissionRepository {
    private readonly DbContext _context;

    public UserGlobalPermissionRepository(DbContext context) {
        _context = context;
    }

    public List<Permission> GetForUser(Guid userId) {
        return _context.UserGlobalPermissions
            .InnerJoin(_context.Permissions, (gp, p) => gp.UserId == userId && gp.PermissionId == p.Id, (gp, p) => p)
            .ToList();
    }

    public void AddForUser(Guid userId, Permission permission) {
        _context.Insert(new UserGlobalPermission {
            UserId = userId,
            PermissionId = permission.Id,
        });
    }

    public void RemoveForUser(Guid userId, Permission permission) {
        _context.UserGlobalPermissions
            .Where(ugp => ugp.UserId == userId && ugp.PermissionId == permission.Id)
            .Delete();
    }
}