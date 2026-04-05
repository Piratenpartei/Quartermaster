using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Data;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.Roles;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Data.Users;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Roles;

[NotInParallel]
public class RoleRepositoryTests : IDisposable {
    private DbContext _context = default!;
    private RoleRepository _roleRepo = default!;
    private PermissionRepository _permissionRepo = default!;
    private UserGlobalPermissionRepository _globalPermRepo = default!;
    private UserChapterPermissionRepository _chapterPermRepo = default!;

    private Guid _userId;
    private Guid _chapterId;
    private Guid _otherChapterId;

    [Before(Test)]
    public void Setup() {
        TestDatabaseFixture.CleanAllTables();
        _context = TestDatabaseFixture.CreateDbContext();
        _roleRepo = new RoleRepository(_context);
        _permissionRepo = new PermissionRepository(_context);
        _globalPermRepo = new UserGlobalPermissionRepository(_context, _roleRepo);
        _chapterPermRepo = new UserChapterPermissionRepository(_context, _roleRepo);

        _permissionRepo.SupplementDefaults();
        _roleRepo.SupplementDefaults();

        // Seed Null Island admin division (User has FK)
        _context.Insert(new AdministrativeDivision { Id = Guid.Empty, Name = "Null Island", Depth = 0 });

        // Seed a test user (minimal fields)
        _userId = Guid.NewGuid();
        _context.Insert(new User {
            Id = _userId,
            Username = "testuser",
            EMail = "test@example.com",
            FirstName = "Test",
            LastName = "User"
        });

        // Seed two chapters
        _chapterId = Guid.NewGuid();
        _otherChapterId = Guid.NewGuid();
        _context.Insert(new Chapter { Id = _chapterId, Name = "Chapter A", ShortCode = "A", ExternalCode = "A" });
        _context.Insert(new Chapter { Id = _otherChapterId, Name = "Chapter B", ShortCode = "B", ExternalCode = "B" });
    }

    [Test]
    public async Task SupplementDefaults_SeedsChapterOfficerRole() {
        var role = _roleRepo.GetByIdentifier(PermissionIdentifier.SystemRole.ChapterOfficer);
        await Assert.That(role).IsNotNull();
        await Assert.That(role!.IsSystem).IsTrue();
        await Assert.That(role.Scope).IsEqualTo(RoleScope.ChapterScoped);

        var perms = _roleRepo.GetPermissions(role.Id);
        await Assert.That(perms.Count).IsEqualTo(PermissionIdentifier.DefaultOfficerPermissions.Count);
    }

    [Test]
    public async Task Assign_CreatesUniqueAssignment() {
        var role = _roleRepo.GetByIdentifier(PermissionIdentifier.SystemRole.ChapterOfficer)!;

        var a1 = _roleRepo.Assign(_userId, role.Id, _chapterId);
        var a2 = _roleRepo.Assign(_userId, role.Id, _chapterId);

        await Assert.That(a1.Id).IsEqualTo(a2.Id);

        var assignments = _roleRepo.GetAssignmentsForUser(_userId);
        await Assert.That(assignments.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Revoke_RemovesAssignment() {
        var role = _roleRepo.GetByIdentifier(PermissionIdentifier.SystemRole.ChapterOfficer)!;
        _roleRepo.Assign(_userId, role.Id, _chapterId);

        _roleRepo.Revoke(_userId, role.Id, _chapterId);

        var assignments = _roleRepo.GetAssignmentsForUser(_userId);
        await Assert.That(assignments.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ChapterOfficerRole_GrantsChapterPermissions() {
        var role = _roleRepo.GetByIdentifier(PermissionIdentifier.SystemRole.ChapterOfficer)!;
        _roleRepo.Assign(_userId, role.Id, _chapterId);

        // User should have all officer permissions for assigned chapter
        var hasView = _chapterPermRepo.HasPermissionForChapter(_userId, _chapterId, PermissionIdentifier.ViewMembers);
        await Assert.That(hasView).IsTrue();

        // User should NOT have those permissions for the other chapter
        var hasOtherView = _chapterPermRepo.HasPermissionForChapter(_userId, _otherChapterId, PermissionIdentifier.ViewMembers);
        await Assert.That(hasOtherView).IsFalse();
    }

    [Test]
    public async Task Revoke_RemovesPermissionAccess() {
        var role = _roleRepo.GetByIdentifier(PermissionIdentifier.SystemRole.ChapterOfficer)!;
        _roleRepo.Assign(_userId, role.Id, _chapterId);
        await Assert.That(_chapterPermRepo.HasPermissionForChapter(_userId, _chapterId, PermissionIdentifier.ViewEvents)).IsTrue();

        _roleRepo.Revoke(_userId, role.Id, _chapterId);

        var hasAfter = _chapterPermRepo.HasPermissionForChapter(_userId, _chapterId, PermissionIdentifier.ViewEvents);
        await Assert.That(hasAfter).IsFalse();
    }

    [Test]
    public async Task GlobalRole_GrantsGlobalPermissions() {
        // Create a custom global role
        var roleId = Guid.NewGuid();
        _roleRepo.Create(new Role {
            Id = roleId,
            Identifier = "test_global",
            Name = "Test Global",
            Description = "",
            Scope = RoleScope.Global,
            IsSystem = false
        });
        _roleRepo.SetPermissions(roleId, new List<string> { PermissionIdentifier.ViewAudit });
        _roleRepo.Assign(_userId, roleId, null);

        var perms = _globalPermRepo.GetForUser(_userId);
        await Assert.That(perms.Any(p => p.Identifier == PermissionIdentifier.ViewAudit)).IsTrue();
    }

    [Test]
    public async Task DirectAndRoleGrants_AreMerged() {
        // Grant ViewMembers directly for chapter A
        var viewMembersPermission = _permissionRepo.GetByIdentifier(PermissionIdentifier.ViewMembers)!;
        _chapterPermRepo.AddForUser(_userId, _chapterId, viewMembersPermission.Id);

        // Assign Chapter Officer role for chapter B
        var role = _roleRepo.GetByIdentifier(PermissionIdentifier.SystemRole.ChapterOfficer)!;
        _roleRepo.Assign(_userId, role.Id, _otherChapterId);

        var all = _chapterPermRepo.GetAllForUser(_userId);

        // Chapter A should have ViewMembers (from direct grant)
        await Assert.That(all.ContainsKey(_chapterId)).IsTrue();
        await Assert.That(all[_chapterId].Contains(PermissionIdentifier.ViewMembers)).IsTrue();

        // Chapter B should have officer perms (from role)
        await Assert.That(all.ContainsKey(_otherChapterId)).IsTrue();
        await Assert.That(all[_otherChapterId].Contains(PermissionIdentifier.ViewMembers)).IsTrue();
        await Assert.That(all[_otherChapterId].Contains(PermissionIdentifier.ViewEvents)).IsTrue();
    }

    [Test]
    public async Task Delete_RemovesRoleAndAssignments() {
        var roleId = Guid.NewGuid();
        _roleRepo.Create(new Role {
            Id = roleId,
            Identifier = "to_delete",
            Name = "ToDelete",
            Description = "",
            Scope = RoleScope.ChapterScoped,
            IsSystem = false
        });
        _roleRepo.SetPermissions(roleId, new List<string> { PermissionIdentifier.ViewMembers });
        _roleRepo.Assign(_userId, roleId, _chapterId);

        _roleRepo.Delete(roleId);

        await Assert.That(_roleRepo.Get(roleId)).IsNull();
        await Assert.That(_roleRepo.GetAssignmentsForUser(_userId).Count).IsEqualTo(0);
    }

    [Test]
    public async Task SetPermissions_ReplacesExistingList() {
        var roleId = Guid.NewGuid();
        _roleRepo.Create(new Role {
            Id = roleId,
            Identifier = "test_replace",
            Name = "Replace",
            Description = "",
            Scope = RoleScope.ChapterScoped,
            IsSystem = false
        });
        _roleRepo.SetPermissions(roleId, new List<string> { PermissionIdentifier.ViewMembers, PermissionIdentifier.ViewEvents });
        await Assert.That(_roleRepo.GetPermissions(roleId).Count).IsEqualTo(2);

        _roleRepo.SetPermissions(roleId, new List<string> { PermissionIdentifier.ViewMotions });
        var after = _roleRepo.GetPermissions(roleId);
        await Assert.That(after.Count).IsEqualTo(1);
        await Assert.That(after.Contains(PermissionIdentifier.ViewMotions)).IsTrue();
    }

    public void Dispose() {
        _context?.Dispose();
    }
}
