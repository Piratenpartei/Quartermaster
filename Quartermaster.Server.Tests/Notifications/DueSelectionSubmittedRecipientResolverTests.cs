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
public class DueSelectionSubmittedRecipientResolverTests : RepositoryTestBase {
    private TestDataBuilder _builder = default!;
    private DueSelectionSubmittedRecipientResolver _resolver = default!;

    [Before(Test)]
    public void Setup() {
        _builder = new TestDataBuilder(Db);
        var permRepo = new PermissionRepository(Db);
        permRepo.SupplementDefaults();
        new RoleRepository(Db).SupplementDefaults();
        _resolver = new DueSelectionSubmittedRecipientResolver(Db, new ChapterRepository(Db, AuditLog));
    }

    private DueSelectionSubmittedPayload PayloadFor(Guid chapterId) =>
        new(Guid.NewGuid(), chapterId, "C", "First", "Last", 12m);

    [Test]
    public async Task TriggerId_matches_constant() {
        await Assert.That(_resolver.TriggerId).IsEqualTo(NotificationTriggers.DueSelectionSubmitted);
    }

    [Test]
    public async Task Picks_users_with_ProcessDueSelections_on_chapter() {
        var chapter = _builder.SeedChapter("C");
        var (user, _) = _builder.SeedAuthenticatedUser(
            email: "intake@test.local",
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.ProcessDueSelections } });

        var result = _resolver.Resolve(PayloadFor(chapter.Id));
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].UserId).IsEqualTo(user.Id);
    }
}
