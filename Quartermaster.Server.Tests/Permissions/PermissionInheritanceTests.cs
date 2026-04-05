using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Data;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.Roles;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Permissions;

public class PermissionInheritanceTests {
    private WorkerDatabase _db = default!;
    private DbContext _context = default!;
    private TestDataBuilder _builder = default!;
    private ChapterRepository _chapterRepo = default!;
    private UserChapterPermissionRepository _chapterPermRepo = default!;
    private PermissionRepository _permRepo = default!;

    [Before(Test)]
    public void Setup() {
        _db = TestDatabaseFixture.Acquire();
        _db.CleanAllTables();
        _context = _db.CreateDbContext();
        _builder = new TestDataBuilder(_context);
        _chapterRepo = new ChapterRepository(_context);
        _permRepo = new PermissionRepository(_context);
        var roleRepo = new RoleRepository(_context);
        _chapterPermRepo = new UserChapterPermissionRepository(_context, roleRepo);
        _permRepo.SupplementDefaults();
    }

    [After(Test)]
    public void Teardown() {
        _context?.Dispose();
        TestDatabaseFixture.Release(_db);
    }

    [Test]
    public async Task View_permission_inherits_down_from_root_to_leaf() {
        // 4-level hierarchy
        var chain = _builder.SeedChapterHierarchy("Bund", "LV", "Bezirk", "Kreis");
        var (user, _) = _builder.SeedAuthenticatedUser();

        // Grant ViewEvents at root (Bund)
        _builder.GrantChapterPermission(user.Id, chain[0].Id, PermissionIdentifier.ViewEvents);

        // Should inherit to all descendants
        foreach (var chapter in chain) {
            var has = _chapterPermRepo.HasPermissionWithInheritance(user.Id, chapter.Id, PermissionIdentifier.ViewEvents, _chapterRepo);
            await Assert.That(has).IsTrue();
        }
    }

    [Test]
    public async Task View_permission_granted_at_leaf_does_not_inherit_up() {
        var chain = _builder.SeedChapterHierarchy("Bund", "LV", "Kreis");
        var (user, _) = _builder.SeedAuthenticatedUser();

        _builder.GrantChapterPermission(user.Id, chain[2].Id, PermissionIdentifier.ViewEvents);

        // Leaf has it, ancestors do not
        await Assert.That(_chapterPermRepo.HasPermissionWithInheritance(user.Id, chain[2].Id, PermissionIdentifier.ViewEvents, _chapterRepo)).IsTrue();
        await Assert.That(_chapterPermRepo.HasPermissionWithInheritance(user.Id, chain[1].Id, PermissionIdentifier.ViewEvents, _chapterRepo)).IsFalse();
        await Assert.That(_chapterPermRepo.HasPermissionWithInheritance(user.Id, chain[0].Id, PermissionIdentifier.ViewEvents, _chapterRepo)).IsFalse();
    }

    [Test]
    public async Task Write_permission_does_NOT_inherit_down() {
        var chain = _builder.SeedChapterHierarchy("Bund", "LV", "Kreis");
        var (user, _) = _builder.SeedAuthenticatedUser();

        _builder.GrantChapterPermission(user.Id, chain[0].Id, PermissionIdentifier.EditEvents);

        // Has it at root
        await Assert.That(_chapterPermRepo.HasPermissionWithInheritance(user.Id, chain[0].Id, PermissionIdentifier.EditEvents, _chapterRepo)).IsTrue();
        // Does NOT have it at descendants (write perms are exact-match only)
        await Assert.That(_chapterPermRepo.HasPermissionWithInheritance(user.Id, chain[1].Id, PermissionIdentifier.EditEvents, _chapterRepo)).IsFalse();
        await Assert.That(_chapterPermRepo.HasPermissionWithInheritance(user.Id, chain[2].Id, PermissionIdentifier.EditEvents, _chapterRepo)).IsFalse();
    }

    [Test]
    public async Task Permission_inheritance_works_with_deep_hierarchy() {
        // 10-level chain
        var names = Enumerable.Range(0, 10).Select(i => $"Level-{i}").ToArray();
        var chain = _builder.SeedChapterHierarchy(names);
        var (user, _) = _builder.SeedAuthenticatedUser();

        _builder.GrantChapterPermission(user.Id, chain[0].Id, PermissionIdentifier.ViewMotions);

        // All 10 levels should see it
        foreach (var chapter in chain) {
            await Assert.That(_chapterPermRepo.HasPermissionWithInheritance(user.Id, chapter.Id, PermissionIdentifier.ViewMotions, _chapterRepo)).IsTrue();
        }
    }

    [Test]
    public async Task User_without_any_grants_has_no_permissions() {
        var chapter = _builder.SeedChapter("Test");
        var (user, _) = _builder.SeedAuthenticatedUser();

        await Assert.That(_chapterPermRepo.HasPermissionForChapter(user.Id, chapter.Id, PermissionIdentifier.ViewEvents)).IsFalse();
        await Assert.That(_chapterPermRepo.HasPermissionWithInheritance(user.Id, chapter.Id, PermissionIdentifier.ViewEvents, _chapterRepo)).IsFalse();
    }

    [Test]
    public async Task GetAllForUser_returns_direct_grants_grouped_by_chapter() {
        var chA = _builder.SeedChapter("A");
        var chB = _builder.SeedChapter("B");
        var (user, _) = _builder.SeedAuthenticatedUser();

        _builder.GrantChapterPermission(user.Id, chA.Id, PermissionIdentifier.ViewEvents);
        _builder.GrantChapterPermission(user.Id, chA.Id, PermissionIdentifier.EditEvents);
        _builder.GrantChapterPermission(user.Id, chB.Id, PermissionIdentifier.ViewMotions);

        var all = _chapterPermRepo.GetAllForUser(user.Id);
        await Assert.That(all.Count).IsEqualTo(2);
        await Assert.That(all[chA.Id].Count).IsEqualTo(2);
        await Assert.That(all[chA.Id].Contains(PermissionIdentifier.ViewEvents)).IsTrue();
        await Assert.That(all[chA.Id].Contains(PermissionIdentifier.EditEvents)).IsTrue();
        await Assert.That(all[chB.Id].Count).IsEqualTo(1);
        await Assert.That(all[chB.Id].Contains(PermissionIdentifier.ViewMotions)).IsTrue();
    }

    [Test]
    public async Task Role_derived_permissions_are_included() {
        var chapter = _builder.SeedChapter("Test");
        var (user, _) = _builder.SeedAuthenticatedUser();

        var role = _builder.SeedRole("test_role", "Test Role", RoleScope.ChapterScoped);
        _builder.AddPermissionToRole(role.Id, PermissionIdentifier.ViewEvents);
        _builder.AssignRoleToUser(user.Id, role.Id, chapter.Id);

        await Assert.That(_chapterPermRepo.HasPermissionForChapter(user.Id, chapter.Id, PermissionIdentifier.ViewEvents)).IsTrue();
    }

    [Test]
    public async Task Identical_grant_at_multiple_chapters_shows_in_each() {
        var chA = _builder.SeedChapter("A");
        var chB = _builder.SeedChapter("B");
        var (user, _) = _builder.SeedAuthenticatedUser();

        _builder.GrantChapterPermission(user.Id, chA.Id, PermissionIdentifier.ViewEvents);
        _builder.GrantChapterPermission(user.Id, chB.Id, PermissionIdentifier.ViewEvents);

        await Assert.That(_chapterPermRepo.HasPermissionForChapter(user.Id, chA.Id, PermissionIdentifier.ViewEvents)).IsTrue();
        await Assert.That(_chapterPermRepo.HasPermissionForChapter(user.Id, chB.Id, PermissionIdentifier.ViewEvents)).IsTrue();
    }
}
