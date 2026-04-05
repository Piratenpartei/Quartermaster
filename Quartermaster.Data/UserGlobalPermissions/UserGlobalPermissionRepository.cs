using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.Roles;

namespace Quartermaster.Data.UserGlobalPermissions;

public class UserGlobalPermissionRepository {
    private readonly DbContext _context;
    private readonly RoleRepository _roleRepo;

    public UserGlobalPermissionRepository(DbContext context, RoleRepository roleRepo) {
        _context = context;
        _roleRepo = roleRepo;
    }

    public List<Permission> GetForUser(Guid userId) {
        // Direct grants
        var direct = _context.UserGlobalPermissions
            .InnerJoin(_context.Permissions, (gp, p) => gp.UserId == userId && gp.PermissionId == p.Id, (gp, p) => p)
            .ToList();

        // Role-derived grants (from Global-scoped role assignments)
        var roleIdentifiers = _roleRepo.GetGlobalPermissionsViaRoles(userId);
        if (roleIdentifiers.Count == 0)
            return direct;

        var existingIdentifiers = new HashSet<string>(direct.Select(p => p.Identifier));
        var missing = roleIdentifiers.Where(i => !existingIdentifiers.Contains(i)).ToList();
        if (missing.Count == 0)
            return direct;

        var extra = _context.Permissions.Where(p => missing.Contains(p.Identifier)).ToList();
        direct.AddRange(extra);
        return direct;
    }

    public void AddForUser(Guid userId, Permission permission) {
        var exists = _context.UserGlobalPermissions
            .Where(ugp => ugp.UserId == userId && ugp.PermissionId == permission.Id)
            .FirstOrDefault();
        if (exists != null)
            return;

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