using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;
using Quartermaster.Api;

namespace Quartermaster.Data.Roles;

public class RoleRepository {
    private readonly DbContext _context;

    public RoleRepository(DbContext context) {
        _context = context;
    }

    public List<Role> GetAll()
        => _context.Roles.OrderBy(r => r.Name).ToList();

    public Role? Get(Guid id)
        => _context.Roles.Where(r => r.Id == id).FirstOrDefault();

    public Role? GetByIdentifier(string identifier)
        => _context.Roles.Where(r => r.Identifier == identifier).FirstOrDefault();

    public List<string> GetPermissions(Guid roleId)
        => _context.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.PermissionIdentifier)
            .ToList();

    public void Create(Role role) => _context.Insert(role);

    public void SupplementDefaults() {
        // Seed "Chapter Officer" system role (locked, ChapterScoped, inherits to children)
        var officer = GetByIdentifier(PermissionIdentifier.SystemRole.ChapterOfficer);
        if (officer == null) {
            officer = new Role {
                Id = Guid.NewGuid(),
                Identifier = PermissionIdentifier.SystemRole.ChapterOfficer,
                Name = "Vorstand",
                Description = "Systemrolle: Automatisch zugewiesen an eingetragene Vorstandsmitglieder.",
                Scope = RoleScope.ChapterScoped,
                IsSystem = true,
                InheritsToChildren = true
            };
            Create(officer);
        } else if (!officer.InheritsToChildren) {
            // Correct drift — officers should always inherit.
            _context.Roles.Where(r => r.Id == officer.Id)
                .Set(r => r.InheritsToChildren, true).Update();
        }

        // Seed "General Chapter Delegate" system role (locked, ChapterScoped, does NOT inherit)
        var delegateRole = GetByIdentifier(PermissionIdentifier.SystemRole.GeneralChapterDelegate);
        if (delegateRole == null) {
            delegateRole = new Role {
                Id = Guid.NewGuid(),
                Identifier = PermissionIdentifier.SystemRole.GeneralChapterDelegate,
                Name = "Delegierter",
                Description = "Systemrolle: Delegierter einer Gliederung. Gleiche Rechte wie Vorstand, aber ohne Vererbung in untergeordnete Gliederungen.",
                Scope = RoleScope.ChapterScoped,
                IsSystem = true,
                InheritsToChildren = false
            };
            Create(delegateRole);
        } else if (delegateRole.InheritsToChildren) {
            // Correct drift — delegates must never inherit.
            _context.Roles.Where(r => r.Id == delegateRole.Id)
                .Set(r => r.InheritsToChildren, false).Update();
        }

        // Always refresh permissions on locked system roles to match DefaultOfficerPermissions.
        SetPermissions(officer.Id, PermissionIdentifier.DefaultOfficerPermissions);
        SetPermissions(delegateRole.Id, PermissionIdentifier.DefaultOfficerPermissions);
    }

    public void Update(Role role) {
        _context.Roles
            .Where(r => r.Id == role.Id)
            .Set(r => r.Name, role.Name)
            .Set(r => r.Description, role.Description)
            .Update();
    }

    public void Delete(Guid roleId) {
        _context.RolePermissions.Where(rp => rp.RoleId == roleId).Delete();
        _context.UserRoleAssignments.Where(a => a.RoleId == roleId).Delete();
        _context.Roles.Where(r => r.Id == roleId).Delete();
    }

    public void SetPermissions(Guid roleId, List<string> permissionIdentifiers) {
        _context.RolePermissions.Where(rp => rp.RoleId == roleId).Delete();
        foreach (var permId in permissionIdentifiers.Distinct()) {
            _context.Insert(new RolePermission {
                RoleId = roleId,
                PermissionIdentifier = permId
            });
        }
    }

    // ---------- Assignments ----------

    public List<UserRoleAssignment> GetAssignmentsForUser(Guid userId)
        => _context.UserRoleAssignments.Where(a => a.UserId == userId).ToList();

    public List<UserRoleAssignment> GetAllAssignments()
        => _context.UserRoleAssignments.ToList();

    public UserRoleAssignment? GetAssignment(Guid id)
        => _context.UserRoleAssignments.Where(a => a.Id == id).FirstOrDefault();

    /// <summary>
    /// Creates an assignment if one doesn't already exist for (userId, roleId, chapterId).
    /// Returns the existing or newly-created assignment.
    /// </summary>
    public UserRoleAssignment Assign(Guid userId, Guid roleId, Guid? chapterId) {
        var existing = _context.UserRoleAssignments
            .Where(a => a.UserId == userId && a.RoleId == roleId && a.ChapterId == chapterId)
            .FirstOrDefault();
        if (existing != null)
            return existing;

        var assignment = new UserRoleAssignment {
            UserId = userId,
            RoleId = roleId,
            ChapterId = chapterId
        };
        _context.Insert(assignment);
        return assignment;
    }

    public void Revoke(Guid userId, Guid roleId, Guid? chapterId) {
        _context.UserRoleAssignments
            .Where(a => a.UserId == userId && a.RoleId == roleId && a.ChapterId == chapterId)
            .Delete();
    }

    public void RevokeAssignment(Guid assignmentId) {
        _context.UserRoleAssignments.Where(a => a.Id == assignmentId).Delete();
    }

    /// <summary>
    /// Returns the set of permission identifiers a user has via role assignments
    /// for a given chapter (includes Global roles too). Used for direct (exact-chapter)
    /// permission checks — the role's InheritsToChildren flag is ignored here.
    /// </summary>
    public HashSet<string> GetChapterPermissionsViaRoles(Guid userId, Guid chapterId) {
        var result = (
            from a in _context.UserRoleAssignments
            join rp in _context.RolePermissions on a.RoleId equals rp.RoleId
            where a.UserId == userId
                && (a.ChapterId == chapterId || a.ChapterId == null)
            select rp.PermissionIdentifier
        ).Distinct().ToList();

        return new HashSet<string>(result);
    }

    /// <summary>
    /// Returns permission identifiers the user has on <paramref name="chapterId"/> via role
    /// assignments, but ONLY from roles with <c>InheritsToChildren = true</c>.
    /// Used by ancestor-chain walks in permission inheritance — permissions from
    /// non-inheriting roles (e.g. delegates) are excluded so they don't bleed into
    /// descendant chapters.
    /// </summary>
    public HashSet<string> GetInheritableChapterPermissionsViaRoles(Guid userId, Guid chapterId) {
        var result = (
            from a in _context.UserRoleAssignments
            join r in _context.Roles on a.RoleId equals r.Id
            join rp in _context.RolePermissions on a.RoleId equals rp.RoleId
            where a.UserId == userId
                && (a.ChapterId == chapterId || a.ChapterId == null)
                && r.InheritsToChildren
            select rp.PermissionIdentifier
        ).Distinct().ToList();

        return new HashSet<string>(result);
    }

    /// <summary>
    /// Returns true if the user holds a direct role assignment to the given chapter for
    /// any of the specified role identifiers. No inheritance applied — this is an
    /// exact-match check on the user-role-assignment's ChapterId.
    /// </summary>
    public bool HasDirectRoleAssignment(Guid userId, Guid chapterId, params string[] roleIdentifiers) {
        if (roleIdentifiers.Length == 0)
            return false;
        var idList = roleIdentifiers.ToList(); // LinqToDB needs List<T> for .Contains translation
        return (
            from a in _context.UserRoleAssignments
            join r in _context.Roles on a.RoleId equals r.Id
            where a.UserId == userId
                && a.ChapterId == chapterId
                && idList.Contains(r.Identifier)
            select a.Id
        ).Any();
    }

    /// <summary>
    /// Returns the set of global permission identifiers a user has via role assignments
    /// (Global-scoped roles only, i.e. ChapterId == null).
    /// </summary>
    public HashSet<string> GetGlobalPermissionsViaRoles(Guid userId) {
        var result = (
            from a in _context.UserRoleAssignments
            join rp in _context.RolePermissions on a.RoleId equals rp.RoleId
            where a.UserId == userId && a.ChapterId == null
            select rp.PermissionIdentifier
        ).Distinct().ToList();

        return new HashSet<string>(result);
    }

    /// <summary>
    /// Returns all (UserId, ChapterId) pairs that grant the given permission via any role assignment.
    /// Global assignments return ChapterId=null (meaning "all chapters").
    /// </summary>
    public List<(Guid UserId, Guid? ChapterId)> GetAssignmentsGranting(string permissionIdentifier) {
        return (
            from a in _context.UserRoleAssignments
            join rp in _context.RolePermissions on a.RoleId equals rp.RoleId
            where rp.PermissionIdentifier == permissionIdentifier
            select new { a.UserId, a.ChapterId }
        ).Distinct().ToList().Select(x => (x.UserId, x.ChapterId)).ToList();
    }

    /// <summary>
    /// Returns chapter IDs where the user has the given permission via a chapter-scoped role assignment.
    /// </summary>
    public List<Guid> GetChapterIdsGrantingPermission(Guid userId, string permissionIdentifier) {
        return (
            from a in _context.UserRoleAssignments
            join rp in _context.RolePermissions on a.RoleId equals rp.RoleId
            where a.UserId == userId
                && rp.PermissionIdentifier == permissionIdentifier
                && a.ChapterId != null
            select a.ChapterId!.Value
        ).Distinct().ToList();
    }

    /// <summary>
    /// Returns true if the user has any Global-scoped role assignment containing the given permission.
    /// </summary>
    public bool HasGlobalPermissionViaRole(Guid userId, string permissionIdentifier) {
        return (
            from a in _context.UserRoleAssignments
            join rp in _context.RolePermissions on a.RoleId equals rp.RoleId
            where a.UserId == userId
                && rp.PermissionIdentifier == permissionIdentifier
                && a.ChapterId == null
            select a.Id
        ).Any();
    }
}
