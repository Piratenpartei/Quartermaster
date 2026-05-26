using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.Motions;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Notifications;

public class MotionSubmittedNotificationTests : IntegrationTestBase {
    private async Task<HttpResponseMessage> SubmitMotion(Guid chapterId, string title) {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        return await client.PostAsJsonAsync("/api/motions", new MotionCreateRequest {
            ChapterId = chapterId,
            AuthorName = "Anonymous Author",
            AuthorEmail = "author@example.com",
            Title = title,
            Text = "Motion body."
        });
    }

    [Test]
    public async Task Writes_one_NotificationLog_per_eligible_recipient() {
        var chapter = Builder.SeedChapter("C");
        var (alice, _) = Builder.SeedAuthenticatedUser(
            email: "alice@test.local",
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMotions } });
        var (bob, _) = Builder.SeedAuthenticatedUser(
            email: "bob@test.local",
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMotions } });
        // Charlie has no perm — should not be notified.
        Builder.SeedAuthenticatedUser(email: "charlie@test.local");

        var response = await SubmitMotion(chapter.Id, "Test Motion");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var logs = Db.NotificationLogs.Where(l => l.TriggerId == "motion_submitted").ToList();
        await Assert.That(logs.Count).IsEqualTo(2);

        var recipientIds = logs.Select(l => l.RecipientUserId).ToHashSet();
        await Assert.That(recipientIds.Contains(alice.Id)).IsTrue();
        await Assert.That(recipientIds.Contains(bob.Id)).IsTrue();
    }

    [Test]
    public async Task Skips_dispatch_when_no_recipients() {
        var chapter = Builder.SeedChapter("C");
        Builder.SeedAuthenticatedUser(email: "nobody@test.local");

        var response = await SubmitMotion(chapter.Id, "Test Motion");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var logs = Db.NotificationLogs.Where(l => l.TriggerId == "motion_submitted").ToList();
        await Assert.That(logs.Count).IsEqualTo(0);
    }

    [Test]
    public async Task NotificationLog_attribution_links_back_to_motion() {
        var chapter = Builder.SeedChapter("C");
        var (_, _) = Builder.SeedAuthenticatedUser(
            email: "officer@test.local",
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMotions } });

        var response = await SubmitMotion(chapter.Id, "Trackable Motion");
        var dto = await response.Content.ReadFromJsonAsync<MotionDTO>();
        await Assert.That(dto).IsNotNull();

        var log = Db.NotificationLogs.First(l => l.TriggerId == "motion_submitted");
        await Assert.That(log.ChannelId).IsEqualTo("smtp");
        await Assert.That(log.SourceEntityType).IsEqualTo("Motion");
        await Assert.That(log.SourceEntityId).IsEqualTo(dto!.Id);
        await Assert.That(log.Subject!.Contains("Trackable Motion")).IsTrue();
        await Assert.That(log.Body!.Contains("Trackable Motion")).IsTrue();
        await Assert.That(log.Body!.Contains("Anonymous Author")).IsTrue();
    }

    [Test]
    public async Task Officer_role_inherits_to_child_chapter() {
        var chain = Builder.SeedChapterHierarchy("Parent", "Child");
        var (parentOfficer, _) = Builder.SeedAuthenticatedUser(
            email: "parent.officer@test.local", firstName: "Parent", lastName: "Officer");
        var roleRepo = new Quartermaster.Data.Roles.RoleRepository(Db);
        var officerRole = roleRepo.GetByIdentifier(PermissionIdentifier.SystemRole.ChapterOfficer)!;
        roleRepo.Assign(parentOfficer.Id, officerRole.Id, chain[0].Id);

        await SubmitMotion(chain[1].Id, "Child Chapter Motion");

        var logs = Db.NotificationLogs.Where(l => l.TriggerId == "motion_submitted").ToList();
        await Assert.That(logs.Count).IsEqualTo(1);
        await Assert.That(logs[0].RecipientUserId).IsEqualTo(parentOfficer.Id);
    }
}
