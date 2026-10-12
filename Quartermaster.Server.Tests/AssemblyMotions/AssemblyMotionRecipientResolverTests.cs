using System;
using System.Linq;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Data;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.Roles;
using Quartermaster.Server.AssemblyMotions.Notifications;
using Quartermaster.Server.Notifications;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.AssemblyMotions;

public class AssemblyMotionRecipientResolverTests : RepositoryTestBase {
    private DbContext _context = default!;
    private TestDataBuilder _builder = default!;
    private RoleRepository _roleRepo = default!;
    private AssemblyMotionRecipientResolver _resolver = default!;

    [Before(Test)]
    public void Setup() {
        _context = Db;
        _builder = new TestDataBuilder(_context);
        new PermissionRepository(_context).SupplementDefaults();
        _roleRepo = new RoleRepository(_context);
        _roleRepo.SupplementDefaults();
        _resolver = new AssemblyMotionRecipientResolver(NotificationTriggers.AssemblyMotionLapsed, _context);
    }

    private static AssemblyMotionNotificationPayload PayloadFor(Guid chapterId, Guid[] memberIds, bool includeCommission)
        => new(Guid.NewGuid(), chapterId, memberIds, includeCommission);

    [Test]
    public async Task Members_resolve_to_linked_users() {
        var chapter = _builder.SeedChapter("C");
        var user = _builder.SeedUser(email: "pirate@test.local");
        var member = _builder.SeedMember(chapterId: chapter.Id, userId: user.Id);

        var result = _resolver.Resolve(PayloadFor(chapter.Id, [member.Id], false));

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].UserId).IsEqualTo(user.Id);
        await Assert.That(result[0].ChannelAddress).IsEqualTo("pirate@test.local");
    }

    [Test]
    public async Task Member_without_user_is_skipped() {
        var chapter = _builder.SeedChapter("C");
        var member = _builder.SeedMember(chapterId: chapter.Id);

        var result = _resolver.Resolve(PayloadFor(chapter.Id, [member.Id], false));

        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task User_without_email_is_skipped() {
        var chapter = _builder.SeedChapter("C");
        var user = _builder.SeedUser(email: "");
        var member = _builder.SeedMember(chapterId: chapter.Id, userId: user.Id);

        var result = _resolver.Resolve(PayloadFor(chapter.Id, [member.Id], false));

        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Commission_is_not_included_unless_requested() {
        var chapter = _builder.SeedChapter("C");
        _builder.SeedAuthenticatedUser(chapterPermissions: new() { [chapter.Id] = [PermissionIdentifier.ManageAssemblyMotions] });

        var result = _resolver.Resolve(PayloadFor(chapter.Id, [], false));

        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Commission_exact_chapter_only() {
        var chain = _builder.SeedChapterHierarchy("Parent", "Child");
        var (onParent, _) = _builder.SeedAuthenticatedUser(
            email: "parent@test.local",
            chapterPermissions: new() { [chain[0].Id] = [PermissionIdentifier.ManageAssemblyMotions] });
        var (global, _) = _builder.SeedAuthenticatedUser(
            email: "global@test.local",
            globalPermissions: [PermissionIdentifier.ManageAssemblyMotions]);
        var (direct, _) = _builder.SeedAuthenticatedUser(
            email: "direct@test.local",
            chapterPermissions: new() { [chain[1].Id] = [PermissionIdentifier.ManageAssemblyMotions] });
        var (viaRole, _) = _builder.SeedAuthenticatedUser(email: "role@test.local");
        var commissionRole = _roleRepo.GetByIdentifier(PermissionIdentifier.SystemRole.MotionCommission)!;
        _roleRepo.Assign(viaRole.Id, commissionRole.Id, chain[1].Id);
        var (roleOnParent, _) = _builder.SeedAuthenticatedUser(email: "roleparent@test.local");
        _roleRepo.Assign(roleOnParent.Id, commissionRole.Id, chain[0].Id);

        var result = _resolver.Resolve(PayloadFor(chain[1].Id, [], true));
        var userIds = result.Select(r => r.UserId!.Value).ToHashSet();

        await Assert.That(userIds.Count).IsEqualTo(3);
        await Assert.That(userIds.Contains(global.Id)).IsTrue();
        await Assert.That(userIds.Contains(direct.Id)).IsTrue();
        await Assert.That(userIds.Contains(viaRole.Id)).IsTrue();
        await Assert.That(userIds.Contains(onParent.Id)).IsFalse();
        await Assert.That(userIds.Contains(roleOnParent.Id)).IsFalse();
    }

    [Test]
    public async Task Duplicates_removed() {
        var chapter = _builder.SeedChapter("C");
        var (user, _) = _builder.SeedAuthenticatedUser(
            email: "double@test.local",
            chapterPermissions: new() { [chapter.Id] = [PermissionIdentifier.ManageAssemblyMotions] });
        var member = _builder.SeedMember(chapterId: chapter.Id, userId: user.Id);
        var commissionRole = _roleRepo.GetByIdentifier(PermissionIdentifier.SystemRole.MotionCommission)!;
        _roleRepo.Assign(user.Id, commissionRole.Id, chapter.Id);

        var result = _resolver.Resolve(PayloadFor(chapter.Id, [member.Id, member.Id], true));

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].UserId).IsEqualTo(user.Id);
    }

    [Test]
    public async Task Wrong_payload_type_resolves_to_nobody() {
        var result = _resolver.Resolve(new { Foo = "bar" });

        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Every_trigger_has_a_resolver_id() {
        await Assert.That(AssemblyMotionRecipientResolver.TriggerIds.Count).IsEqualTo(10);
        await Assert.That(AssemblyMotionRecipientResolver.TriggerIds.Distinct().Count()).IsEqualTo(10);
    }
}
