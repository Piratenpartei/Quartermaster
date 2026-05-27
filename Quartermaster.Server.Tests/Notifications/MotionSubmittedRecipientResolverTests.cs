using System;
using System.Linq;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Data;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.Roles;
using Quartermaster.Server.Notifications;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Notifications;

public class MotionSubmittedRecipientResolverTests : RepositoryTestBase {
    private DbContext _context = default!;
    private TestDataBuilder _builder = default!;
    private ChapterRepository _chapterRepo = default!;
    private RoleRepository _roleRepo = default!;
    private MotionSubmittedRecipientResolver _resolver = default!;

    [Before(Test)]
    public void Setup() {
        _context = Db;
        _builder = new TestDataBuilder(_context);
        _chapterRepo = new ChapterRepository(_context, AuditLog);
        var permRepo = new PermissionRepository(_context);
        permRepo.SupplementDefaults();
        _roleRepo = new RoleRepository(_context);
        _roleRepo.SupplementDefaults();
        _resolver = new MotionSubmittedRecipientResolver(_context, _chapterRepo);
    }

    private MotionSubmittedPayload PayloadFor(Guid chapterId) =>
        new(Guid.NewGuid(), chapterId, "Test", "Author", "Chapter");

    [Test]
    public async Task TriggerId_matches_NotificationTriggers_constant() {
        await Assert.That(_resolver.TriggerId).IsEqualTo(NotificationTriggers.MotionSubmitted);
    }

    [Test]
    public async Task Returns_empty_when_no_one_has_permission() {
        var chapter = _builder.SeedChapter("C");
        _builder.SeedAuthenticatedUser();
        var result = _resolver.Resolve(PayloadFor(chapter.Id));
        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Includes_user_with_direct_chapter_permission() {
        var chapter = _builder.SeedChapter("C");
        var (user, _) = _builder.SeedAuthenticatedUser(
            email: "officer@test.local",
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMotions } });

        var result = _resolver.Resolve(PayloadFor(chapter.Id));
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].UserId).IsEqualTo(user.Id);
        await Assert.That(result[0].ChannelAddress).IsEqualTo("officer@test.local");
    }

    [Test]
    public async Task Includes_user_with_global_role_granting_EditMotions() {
        var chapter = _builder.SeedChapter("C");
        var (user, _) = _builder.SeedAuthenticatedUser(
            email: "admin@test.local",
            globalPermissions: new[] { PermissionIdentifier.EditMotions });

        var result = _resolver.Resolve(PayloadFor(chapter.Id));
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].UserId).IsEqualTo(user.Id);
    }

    [Test]
    public async Task Includes_chapter_officer_via_default_role_permissions() {
        // Officers get DefaultOfficerPermissions which includes EditMotions.
        var chapter = _builder.SeedChapter("C");
        var (user, _) = _builder.SeedAuthenticatedUser(email: "officer@test.local");
        var officerRole = _roleRepo.GetByIdentifier(PermissionIdentifier.SystemRole.ChapterOfficer)!;
        _roleRepo.Assign(user.Id, officerRole.Id, chapter.Id);

        var result = _resolver.Resolve(PayloadFor(chapter.Id));
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].UserId).IsEqualTo(user.Id);
    }

    [Test]
    public async Task Includes_user_without_email_so_dispatcher_can_route_to_other_channels() {
        var chapter = _builder.SeedChapter("C");
        var (user, _) = _builder.SeedAuthenticatedUser(
            email: "",
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMotions } });

        var result = _resolver.Resolve(PayloadFor(chapter.Id));
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].UserId).IsEqualTo(user.Id);
        await Assert.That(result[0].ChannelAddress).IsEqualTo("");
    }

    [Test]
    public async Task Inherits_via_officer_role_on_parent_chapter() {
        // ChapterOfficer role has InheritsToChildren = true, so an officer of the parent
        // chapter is also a recipient for motions submitted to the child.
        var chain = _builder.SeedChapterHierarchy("Parent", "Child");
        var (parentOfficer, _) = _builder.SeedAuthenticatedUser(
            email: "parent.officer@test.local", firstName: "Parent", lastName: "Officer");
        var officerRole = _roleRepo.GetByIdentifier(PermissionIdentifier.SystemRole.ChapterOfficer)!;
        _roleRepo.Assign(parentOfficer.Id, officerRole.Id, chain[0].Id);

        var result = _resolver.Resolve(PayloadFor(chain[1].Id));
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].UserId).IsEqualTo(parentOfficer.Id);
    }

    [Test]
    public async Task Does_not_inherit_via_delegate_role_on_parent_chapter() {
        // GeneralChapterDelegate has InheritsToChildren = false — must NOT pick up child-chapter motions.
        var chain = _builder.SeedChapterHierarchy("Parent", "Child");
        var (delegateUser, _) = _builder.SeedAuthenticatedUser(email: "delegate@test.local");
        var delegateRole = _roleRepo.GetByIdentifier(PermissionIdentifier.SystemRole.GeneralChapterDelegate)!;
        _roleRepo.Assign(delegateUser.Id, delegateRole.Id, chain[0].Id);

        var result = _resolver.Resolve(PayloadFor(chain[1].Id));
        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Deduplicates_user_with_both_direct_and_role_grant() {
        var chapter = _builder.SeedChapter("C");
        var (user, _) = _builder.SeedAuthenticatedUser(
            email: "double@test.local",
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMotions } });
        var officerRole = _roleRepo.GetByIdentifier(PermissionIdentifier.SystemRole.ChapterOfficer)!;
        _roleRepo.Assign(user.Id, officerRole.Id, chapter.Id);

        var result = _resolver.Resolve(PayloadFor(chapter.Id));
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].UserId).IsEqualTo(user.Id);
    }

    [Test]
    public async Task Returns_empty_for_wrong_payload_type() {
        var result = _resolver.Resolve(new { Foo = "bar" });
        await Assert.That(result.Count).IsEqualTo(0);
    }
}
