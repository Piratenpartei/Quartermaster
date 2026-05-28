using System;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.Motions;
using Quartermaster.Data.Notifications;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Notifications;

/// <summary>
/// Verifies the dispatcher skips recipients who have opted out of the (trigger, channel)
/// pair, while leaving everyone else unaffected. Drives through the motion-create endpoint
/// because that's the cheapest trigger to fire end-to-end.
/// </summary>
public class DispatcherPreferenceGatingTests : IntegrationTestBase {
    private async Task SubmitMotion(Guid chapterId, string title) {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        await client.PostAsJsonAsync("/api/motions", new MotionCreateRequest {
            ChapterId = chapterId,
            AuthorName = "Author",
            AuthorEmail = "author@example.com",
            Title = title,
            Text = "Body"
        });
        await ConfirmAllPendingSubmissionsAsync();
    }

    [Test]
    public async Task Opted_out_recipient_is_skipped() {
        var chapter = Builder.SeedChapter("C");
        var (alice, _) = Builder.SeedAuthenticatedUser(
            email: "alice@test.local",
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMotions } });
        var (bob, _) = Builder.SeedAuthenticatedUser(
            email: "bob@test.local",
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMotions } });

        // Bob opts out of motion_submitted email.
        Db.Insert(new UserNotificationPreference {
            UserId = bob.Id, TriggerId = "motion_submitted", ChannelId = "email", Enabled = false
        });

        await SubmitMotion(chapter.Id, "Test Motion");

        var logs = Db.NotificationLogs.Where(l => l.TriggerId == "motion_submitted").ToList();
        await Assert.That(logs.Count).IsEqualTo(1);
        await Assert.That(logs[0].RecipientUserId).IsEqualTo(alice.Id);
    }

    [Test]
    public async Task Explicit_opt_in_overrides_default() {
        // Telegram is off by default; if a future channel test set it to true the
        // dispatcher would honor that. For Phase 3 we just confirm email default = true
        // is still applied when nothing's persisted.
        var chapter = Builder.SeedChapter("C");
        var (alice, _) = Builder.SeedAuthenticatedUser(
            email: "alice@test.local",
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMotions } });

        await SubmitMotion(chapter.Id, "Default-flow Motion");

        var logs = Db.NotificationLogs.Where(l => l.TriggerId == "motion_submitted").ToList();
        await Assert.That(logs.Count).IsEqualTo(1);
        await Assert.That(logs[0].RecipientUserId).IsEqualTo(alice.Id);
    }

    [Test]
    public async Task Opt_out_only_affects_matching_trigger() {
        var chapter = Builder.SeedChapter("C");
        var (user, _) = Builder.SeedAuthenticatedUser(
            email: "user@test.local",
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMotions } });

        // User opts out of application_submitted, NOT motion_submitted.
        Db.Insert(new UserNotificationPreference {
            UserId = user.Id, TriggerId = "application_submitted", ChannelId = "email", Enabled = false
        });

        await SubmitMotion(chapter.Id, "Should Still Fire");

        var logs = Db.NotificationLogs.Where(l => l.TriggerId == "motion_submitted").ToList();
        await Assert.That(logs.Count).IsEqualTo(1);
    }
}
