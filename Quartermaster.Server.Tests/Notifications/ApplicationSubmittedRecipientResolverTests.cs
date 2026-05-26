using System;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Data;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.Roles;
using Quartermaster.Server.Notifications;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Notifications;

/// <summary>Smoke tests — bulk of resolver behavior is covered by <c>MotionSubmittedRecipientResolverTests</c> via the shared base.</summary>
public class ApplicationSubmittedRecipientResolverTests : RepositoryTestBase {
    private TestDataBuilder _builder = default!;
    private ApplicationSubmittedRecipientResolver _resolver = default!;

    [Before(Test)]
    public void Setup() {
        _builder = new TestDataBuilder(Db);
        var permRepo = new PermissionRepository(Db);
        permRepo.SupplementDefaults();
        new RoleRepository(Db).SupplementDefaults();
        _resolver = new ApplicationSubmittedRecipientResolver(Db, new ChapterRepository(Db, AuditLog));
    }

    private ApplicationSubmittedPayload PayloadFor(Guid chapterId) =>
        new(Guid.NewGuid(), chapterId, "C", "First", "Last", false);

    [Test]
    public async Task TriggerId_matches_constant() {
        await Assert.That(_resolver.TriggerId).IsEqualTo(NotificationTriggers.ApplicationSubmitted);
    }

    [Test]
    public async Task Picks_users_with_ProcessApplications_on_chapter() {
        var chapter = _builder.SeedChapter("C");
        var (user, _) = _builder.SeedAuthenticatedUser(
            email: "intake@test.local",
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.ProcessApplications } });
        _builder.SeedAuthenticatedUser(email: "bystander@test.local");

        var result = _resolver.Resolve(PayloadFor(chapter.Id));
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].UserId).IsEqualTo(user.Id);
    }

    [Test]
    public async Task Ignores_users_with_only_EditMotions() {
        var chapter = _builder.SeedChapter("C");
        _builder.SeedAuthenticatedUser(
            email: "motions@test.local",
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMotions } });

        var result = _resolver.Resolve(PayloadFor(chapter.Id));
        await Assert.That(result.Count).IsEqualTo(0);
    }
}
