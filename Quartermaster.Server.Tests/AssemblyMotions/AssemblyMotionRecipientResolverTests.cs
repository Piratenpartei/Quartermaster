using System.Linq;
using System.Threading.Tasks;
using System;
using LinqToDB;
using Quartermaster.Api.Events;
using Quartermaster.Api;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Events;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.Roles;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Data;
using Quartermaster.Server.AssemblyMotions.Notifications;
using Quartermaster.Server.Events;
using Quartermaster.Server.Notifications;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.AssemblyMotions;

public class AssemblyMotionRecipientResolverTests : RepositoryTestBase {
    private DbContext _context = default!;
    private TestDataBuilder _builder = default!;
    private RoleRepository _roleRepo = default!;
    private AssemblyMotionRecipientResolver _resolver = default!;
    private EventUserVisibility _visibility = default!;

    [Before(Test)]
    public void Setup() {
        _context = Db;
        _builder = new TestDataBuilder(_context);
        new PermissionRepository(_context).SupplementDefaults();
        _roleRepo = new RoleRepository(_context);
        _roleRepo.SupplementDefaults();
        _visibility = new EventUserVisibility(
            new UserGlobalPermissionRepository(_context, _roleRepo),
            new UserChapterPermissionRepository(_context, _roleRepo),
            new ChapterRepository(_context, AuditLog));
        _resolver = new AssemblyMotionRecipientResolver(NotificationTriggers.AssemblyMotionLapsed, _context, _visibility);
    }

    private Guid SubscribedUser(Event ev) {
        var user = _builder.SeedUser();
        _context.Insert(new EventSubscription { EventId = ev.Id, UserId = user.Id, CreatedAt = DateTime.UtcNow });
        return user.Id;
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
        await Assert.That(AssemblyMotionRecipientResolver.TriggerIds.Count).IsEqualTo(14);
        await Assert.That(AssemblyMotionRecipientResolver.TriggerIds.Distinct().Count()).IsEqualTo(14);
    }

    [Test]
    public async Task Subscribers_are_added_when_the_event_is_given() {
        var chapter = _builder.SeedChapter("C");
        var ev = _builder.SeedEvent(chapter.Id, visibility: EventVisibility.Public);
        var subscriber = SubscribedUser(ev);

        var result = _resolver.Resolve(new AssemblyMotionNotificationPayload(Guid.NewGuid(), chapter.Id, [], true, ev.Id));

        await Assert.That(result.Select(r => r.UserId).Contains(subscriber)).IsTrue();
    }

    [Test]
    public async Task Subscriber_without_visibility_is_left_out() {
        var chapter = _builder.SeedChapter("C");
        var ev = _builder.SeedEvent(chapter.Id, visibility: EventVisibility.Private);
        var subscriber = SubscribedUser(ev);

        var result = _resolver.Resolve(new AssemblyMotionNotificationPayload(Guid.NewGuid(), chapter.Id, [], true, ev.Id));

        await Assert.That(result.Select(r => r.UserId).Contains(subscriber)).IsFalse();
    }

    [Test]
    public async Task Commission_member_also_subscribed_resolves_once() {
        var chapter = _builder.SeedChapter("C");
        var ev = _builder.SeedEvent(chapter.Id, visibility: EventVisibility.Public);
        var (commission, _) = _builder.SeedAuthenticatedUser(chapterPermissions: new() { [chapter.Id] = [PermissionIdentifier.ManageAssemblyMotions] });
        _context.Insert(new EventSubscription { EventId = ev.Id, UserId = commission.Id, CreatedAt = DateTime.UtcNow });

        var result = _resolver.Resolve(new AssemblyMotionNotificationPayload(Guid.NewGuid(), chapter.Id, [], true, ev.Id));

        await Assert.That(result.Count(r => r.UserId == commission.Id)).IsEqualTo(1);
    }

    [Test]
    public async Task Subscribers_ignored_without_event_id() {
        var chapter = _builder.SeedChapter("C");
        var ev = _builder.SeedEvent(chapter.Id, visibility: EventVisibility.Public);
        var subscriber = SubscribedUser(ev);

        var result = _resolver.Resolve(PayloadFor(chapter.Id, [], true));

        await Assert.That(result.Select(r => r.UserId).Contains(subscriber)).IsFalse();
    }

    [Test]
    public async Task Resolves_payload_user_ids() {
        var chapter = _builder.SeedChapter("C");
        var kept = _builder.SeedUser(email: "kept@test.local");
        var deleted = _builder.SeedUser(email: "deleted@test.local");
        var noEmail = _builder.SeedUser(email: "");
        _context.Users.Where(u => u.Id == deleted.Id).Set(u => u.DeletedAt, DateTime.UtcNow).Update();
        var payload = new AssemblyMotionNotificationPayload(Guid.NewGuid(), chapter.Id, [], false, UserIds: [kept.Id, deleted.Id, noEmail.Id, kept.Id]);

        var result = _resolver.Resolve(payload);

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].UserId).IsEqualTo(kept.Id);
        await Assert.That(result[0].ChannelAddress).IsEqualTo("kept@test.local");
    }
}
