using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Data;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.Roles;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Permissions;

/// <summary>
/// Tests the <see cref="Role.InheritsToChildren"/> flag: roles flagged as inheriting
/// propagate their permissions down the chapter tree via ancestor-walk, roles flagged
/// non-inheriting only grant access to their exact assigned chapter.
/// </summary>
public class RoleInheritanceFlagTests {
    private WorkerDatabase _db = default!;
    private DbContext _context = default!;
    private TestDataBuilder _builder = default!;
    private ChapterRepository _chapterRepo = default!;
    private UserChapterPermissionRepository _chapterPermRepo = default!;
    private RoleRepository _roleRepo = default!;

    [Before(Test)]
    public void Setup() {
        _db = TestDatabaseFixture.Acquire();
        _db.CleanAllTables();
        _context = _db.CreateDbContext();
        _builder = new TestDataBuilder(_context);
        _chapterRepo = new ChapterRepository(_context);
        var permRepo = new PermissionRepository(_context);
        permRepo.SupplementDefaults();
        _roleRepo = new RoleRepository(_context);
        _roleRepo.SupplementDefaults(); // seeds both chapter_officer and general_chapter_delegate
        _chapterPermRepo = new UserChapterPermissionRepository(_context, _roleRepo);
    }

    [After(Test)]
    public void Teardown() {
        _context?.Dispose();
        TestDatabaseFixture.Release(_db);
    }

    [Test]
    public async Task Officer_role_permission_inherits_to_child_chapter() {
        var chain = _builder.SeedChapterHierarchy("Parent", "Child");
        var (user, _) = _builder.SeedAuthenticatedUser();

        var officerRole = _roleRepo.GetByIdentifier(PermissionIdentifier.SystemRole.ChapterOfficer)!;
        _roleRepo.Assign(user.Id, officerRole.Id, chain[0].Id);

        // ViewMotions is in DefaultOfficerPermissions → granted via officer role on parent chapter.
        // Should inherit to child chapter.
        var hasOnParent = _chapterPermRepo.HasPermissionWithInheritance(
            user.Id, chain[0].Id, PermissionIdentifier.ViewMotions, _chapterRepo);
        var hasOnChild = _chapterPermRepo.HasPermissionWithInheritance(
            user.Id, chain[1].Id, PermissionIdentifier.ViewMotions, _chapterRepo);

        await Assert.That(hasOnParent).IsTrue();
        await Assert.That(hasOnChild).IsTrue();
    }

    [Test]
    public async Task Delegate_role_permission_does_NOT_inherit_to_child_chapter() {
        var chain = _builder.SeedChapterHierarchy("Parent", "Child");
        var (user, _) = _builder.SeedAuthenticatedUser();

        var delegateRole = _roleRepo.GetByIdentifier(PermissionIdentifier.SystemRole.GeneralChapterDelegate)!;
        _roleRepo.Assign(user.Id, delegateRole.Id, chain[0].Id);

        // Delegate has ViewMotions on parent chapter, but must NOT see child chapter.
        var hasOnParent = _chapterPermRepo.HasPermissionWithInheritance(
            user.Id, chain[0].Id, PermissionIdentifier.ViewMotions, _chapterRepo);
        var hasOnChild = _chapterPermRepo.HasPermissionWithInheritance(
            user.Id, chain[1].Id, PermissionIdentifier.ViewMotions, _chapterRepo);

        await Assert.That(hasOnParent).IsTrue();
        await Assert.That(hasOnChild).IsFalse();
    }

    [Test]
    public async Task Direct_UserChapterPermission_still_inherits_normally() {
        // Regression guard: the flag only affects role-derived permissions.
        // Direct UserChapterPermission grants continue to inherit via ancestor walk.
        var chain = _builder.SeedChapterHierarchy("Parent", "Child");
        var (user, _) = _builder.SeedAuthenticatedUser();

        _builder.GrantChapterPermission(user.Id, chain[0].Id, PermissionIdentifier.ViewMotions);

        var hasOnChild = _chapterPermRepo.HasPermissionWithInheritance(
            user.Id, chain[1].Id, PermissionIdentifier.ViewMotions, _chapterRepo);
        await Assert.That(hasOnChild).IsTrue();
    }

    [Test]
    public async Task Delegate_role_direct_chapter_access_still_works() {
        // Delegate has no inheritance but must still work for their exact chapter.
        var chapter = _builder.SeedChapter("Solo");
        var (user, _) = _builder.SeedAuthenticatedUser();

        var delegateRole = _roleRepo.GetByIdentifier(PermissionIdentifier.SystemRole.GeneralChapterDelegate)!;
        _roleRepo.Assign(user.Id, delegateRole.Id, chapter.Id);

        var hasDirect = _chapterPermRepo.HasPermissionForChapter(
            user.Id, chapter.Id, PermissionIdentifier.ViewMotions);
        await Assert.That(hasDirect).IsTrue();
    }

    [Test]
    public async Task HasDirectRoleAssignment_matches_exact_chapter_only() {
        var chain = _builder.SeedChapterHierarchy("Parent", "Child");
        var (user, _) = _builder.SeedAuthenticatedUser();

        var officerRole = _roleRepo.GetByIdentifier(PermissionIdentifier.SystemRole.ChapterOfficer)!;
        _roleRepo.Assign(user.Id, officerRole.Id, chain[0].Id);

        await Assert.That(_roleRepo.HasDirectRoleAssignment(
            user.Id, chain[0].Id, PermissionIdentifier.SystemRole.ChapterOfficer)).IsTrue();
        // Exact-chapter check: officer of parent is NOT a direct officer of child.
        await Assert.That(_roleRepo.HasDirectRoleAssignment(
            user.Id, chain[1].Id, PermissionIdentifier.SystemRole.ChapterOfficer)).IsFalse();
    }

    [Test]
    public async Task HasDirectRoleAssignment_matches_any_of_specified_roles() {
        var chapter = _builder.SeedChapter("Solo");
        var (user, _) = _builder.SeedAuthenticatedUser();

        var delegateRole = _roleRepo.GetByIdentifier(PermissionIdentifier.SystemRole.GeneralChapterDelegate)!;
        _roleRepo.Assign(user.Id, delegateRole.Id, chapter.Id);

        // User is delegate but NOT officer; checking for either role should return true.
        await Assert.That(_roleRepo.HasDirectRoleAssignment(user.Id, chapter.Id,
            PermissionIdentifier.SystemRole.ChapterOfficer,
            PermissionIdentifier.SystemRole.GeneralChapterDelegate)).IsTrue();
        // Checking ONLY for officer role returns false.
        await Assert.That(_roleRepo.HasDirectRoleAssignment(user.Id, chapter.Id,
            PermissionIdentifier.SystemRole.ChapterOfficer)).IsFalse();
    }

    [Test]
    public async Task SupplementDefaults_seeds_both_officer_and_delegate_roles() {
        var officer = _roleRepo.GetByIdentifier(PermissionIdentifier.SystemRole.ChapterOfficer);
        var delegateRole = _roleRepo.GetByIdentifier(PermissionIdentifier.SystemRole.GeneralChapterDelegate);

        await Assert.That(officer).IsNotNull();
        await Assert.That(officer!.IsSystem).IsTrue();
        await Assert.That(officer.InheritsToChildren).IsTrue();

        await Assert.That(delegateRole).IsNotNull();
        await Assert.That(delegateRole!.IsSystem).IsTrue();
        await Assert.That(delegateRole.InheritsToChildren).IsFalse();

        // Both have the DefaultOfficerPermissions set.
        var officerPerms = _roleRepo.GetPermissions(officer.Id);
        var delegatePerms = _roleRepo.GetPermissions(delegateRole.Id);
        await Assert.That(officerPerms.Count).IsEqualTo(PermissionIdentifier.DefaultOfficerPermissions.Count);
        await Assert.That(delegatePerms.Count).IsEqualTo(PermissionIdentifier.DefaultOfficerPermissions.Count);
    }
}
