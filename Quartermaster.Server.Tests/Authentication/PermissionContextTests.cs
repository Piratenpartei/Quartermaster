using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
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

public class PermissionContextTests : RepositoryTestBase {
    private DbContext _context = default!;
    private TestDataBuilder _builder = default!;
    private ChapterRepository _chapterRepo = default!;
    private UserGlobalPermissionRepository _globalRepo = default!;
    private UserChapterPermissionRepository _chapterPermsRepo = default!;

    [Before(Test)]
    public void Setup() {
        _context = Db;
        _builder = new TestDataBuilder(_context);
        _chapterRepo = new ChapterRepository(_context, AuditLog);
        var permRepo = new PermissionRepository(_context);
        permRepo.SupplementDefaults();
        var roleRepo = new RoleRepository(_context);
        _globalRepo = new UserGlobalPermissionRepository(_context, roleRepo);
        _chapterPermsRepo = new UserChapterPermissionRepository(_context, roleRepo);
    }

    private PermissionContext ContextFor(System.Guid? userId) {
        var perms = new PermissionContext(
            new HttpContextAccessor(),
            _globalRepo, _chapterPermsRepo, _chapterRepo);
        var identity = userId.HasValue
            ? new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()) }, "Test")
            : new ClaimsIdentity();
        perms.Bind(new ClaimsPrincipal(identity));
        return perms;
    }

    [Test]
    public async Task HasGlobal_returns_false_for_ungranted() {
        var (user, _) = _builder.SeedAuthenticatedUser();
        var perms = ContextFor(user.Id);
        await Assert.That(perms.HasGlobal(PermissionIdentifier.ViewUsers)).IsFalse();
    }

    [Test]
    public async Task HasGlobal_returns_true_for_granted() {
        var (user, _) = _builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewUsers });
        var perms = ContextFor(user.Id);
        await Assert.That(perms.HasGlobal(PermissionIdentifier.ViewUsers)).IsTrue();
    }

    [Test]
    public async Task GetPermittedChapterIds_returns_null_when_global_permission_granted() {
        var (user, _) = _builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewAllMembers });
        _builder.SeedChapter("A");
        _builder.SeedChapter("B");
        var perms = ContextFor(user.Id);
        var result = perms.GetPermittedChapterIds(
            PermissionIdentifier.ViewAllMembers, PermissionIdentifier.ViewMembers);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetPermittedChapterIds_returns_empty_when_no_permissions() {
        var (user, _) = _builder.SeedAuthenticatedUser();
        _builder.SeedChapter("A");
        var perms = ContextFor(user.Id);
        var result = perms.GetPermittedChapterIds(
            PermissionIdentifier.ViewAllMembers, PermissionIdentifier.ViewMembers);
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetPermittedChapterIds_includes_descendants_of_granted_chapters() {
        var chain = _builder.SeedChapterHierarchy("Root", "Mid", "Leaf");
        var (user, _) = _builder.SeedAuthenticatedUser();
        _builder.GrantChapterPermission(user.Id, chain[0].Id, PermissionIdentifier.ViewMembers);

        var perms = ContextFor(user.Id);
        var result = perms.GetPermittedChapterIds(
            PermissionIdentifier.ViewAllMembers, PermissionIdentifier.ViewMembers);
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

        var perms = ContextFor(user.Id);
        var result = perms.GetPermittedChapterIds(
            PermissionIdentifier.ViewAllMembers, PermissionIdentifier.ViewMembers);
        await Assert.That(result!.Count).IsEqualTo(1);
        await Assert.That(result!.Contains(chain[2].Id)).IsTrue();
    }

    [Test]
    public async Task Has_returns_true_for_global_grant() {
        var chapter = _builder.SeedChapter("C");
        var (user, _) = _builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewMembers });
        var perms = ContextFor(user.Id);
        await Assert.That(perms.Has(chapter.Id, PermissionIdentifier.ViewMembers)).IsTrue();
    }

    [Test]
    public async Task Has_returns_true_for_chapter_grant_with_inheritance() {
        var chain = _builder.SeedChapterHierarchy("Parent", "Child");
        var (user, _) = _builder.SeedAuthenticatedUser();
        _builder.GrantChapterPermission(user.Id, chain[0].Id, PermissionIdentifier.ViewMembers);
        var perms = ContextFor(user.Id);
        await Assert.That(perms.Has(chain[1].Id, PermissionIdentifier.ViewMembers)).IsTrue();
    }

    [Test]
    public async Task HasExact_returns_false_for_parent_chapter_grant() {
        var chain = _builder.SeedChapterHierarchy("Parent", "Child");
        var (user, _) = _builder.SeedAuthenticatedUser();
        _builder.GrantChapterPermission(user.Id, chain[0].Id, PermissionIdentifier.VoteMotions);
        var perms = ContextFor(user.Id);
        await Assert.That(perms.HasExact(chain[1].Id, PermissionIdentifier.VoteMotions)).IsFalse();
        await Assert.That(perms.HasExact(chain[0].Id, PermissionIdentifier.VoteMotions)).IsTrue();
    }

    [Test]
    public async Task UserId_is_null_for_anonymous_principal() {
        var perms = ContextFor(null);
        await Assert.That(perms.UserId).IsNull();
        await Assert.That(perms.HasGlobal(PermissionIdentifier.ViewUsers)).IsFalse();
    }
}
