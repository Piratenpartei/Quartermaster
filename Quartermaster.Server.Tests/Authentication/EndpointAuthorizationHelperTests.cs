using System.Linq;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Data;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.Roles;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Authentication;

public class EndpointAuthorizationHelperTests {
    private WorkerDatabase _db = default!;
    private DbContext _context = default!;
    private TestDataBuilder _builder = default!;
    private ChapterRepository _chapterRepo = default!;
    private UserGlobalPermissionRepository _globalRepo = default!;
    private UserChapterPermissionRepository _chapterRepo_perms = default!;

    [Before(Test)]
    public void Setup() {
        _db = TestDatabaseFixture.Acquire();
        _db.CleanAllTables();
        _context = _db.CreateDbContext();
        _builder = new TestDataBuilder(_context);
        _chapterRepo = new ChapterRepository(_context);
        var permRepo = new PermissionRepository(_context);
        permRepo.SupplementDefaults();
        var roleRepo = new RoleRepository(_context);
        _globalRepo = new UserGlobalPermissionRepository(_context, roleRepo);
        _chapterRepo_perms = new UserChapterPermissionRepository(_context, roleRepo);
    }

    [After(Test)]
    public void Teardown() {
        _context?.Dispose();
        TestDatabaseFixture.Release(_db);
    }

    [Test]
    public async Task HasGlobalPermission_returns_false_for_ungranted() {
        var (user, _) = _builder.SeedAuthenticatedUser();
        var result = EndpointAuthorizationHelper.HasGlobalPermission(user.Id, PermissionIdentifier.ViewUsers, _globalRepo);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task HasGlobalPermission_returns_true_for_granted() {
        var (user, _) = _builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewUsers });
        var result = EndpointAuthorizationHelper.HasGlobalPermission(user.Id, PermissionIdentifier.ViewUsers, _globalRepo);
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task GetPermittedChapterIds_returns_null_when_global_permission_granted() {
        var (user, _) = _builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewAllMembers });
        _builder.SeedChapter("A");
        _builder.SeedChapter("B");
        var result = EndpointAuthorizationHelper.GetPermittedChapterIds(
            user.Id,
            PermissionIdentifier.ViewAllMembers,
            PermissionIdentifier.ViewMembers,
            _globalRepo, _chapterRepo_perms, _chapterRepo);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetPermittedChapterIds_returns_empty_when_no_permissions() {
        var (user, _) = _builder.SeedAuthenticatedUser();
        _builder.SeedChapter("A");
        var result = EndpointAuthorizationHelper.GetPermittedChapterIds(
            user.Id,
            PermissionIdentifier.ViewAllMembers,
            PermissionIdentifier.ViewMembers,
            _globalRepo, _chapterRepo_perms, _chapterRepo);
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetPermittedChapterIds_includes_descendants_of_granted_chapters() {
        var chain = _builder.SeedChapterHierarchy("Root", "Mid", "Leaf");
        var (user, _) = _builder.SeedAuthenticatedUser();
        _builder.GrantChapterPermission(user.Id, chain[0].Id, PermissionIdentifier.ViewMembers);

        var result = EndpointAuthorizationHelper.GetPermittedChapterIds(
            user.Id,
            PermissionIdentifier.ViewAllMembers,
            PermissionIdentifier.ViewMembers,
            _globalRepo, _chapterRepo_perms, _chapterRepo);
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Count).IsEqualTo(3);
        foreach (var ch in chain) {
            await Assert.That(result!.Contains(ch.Id)).IsTrue();
        }
    }

    [Test]
    public async Task GetPermittedChapterIds_only_grant_at_leaf_returns_just_that_chapter() {
        var chain = _builder.SeedChapterHierarchy("Root", "Mid", "Leaf");
        var (user, _) = _builder.SeedAuthenticatedUser();
        _builder.GrantChapterPermission(user.Id, chain[2].Id, PermissionIdentifier.ViewMembers);

        var result = EndpointAuthorizationHelper.GetPermittedChapterIds(
            user.Id,
            PermissionIdentifier.ViewAllMembers,
            PermissionIdentifier.ViewMembers,
            _globalRepo, _chapterRepo_perms, _chapterRepo);
        await Assert.That(result!.Count).IsEqualTo(1);
        await Assert.That(result!.Contains(chain[2].Id)).IsTrue();
    }
}
