using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Notifications;

public class ApplicationSubmittedNotificationTests : IntegrationTestBase {
    private MembershipApplicationDTO Dto(Guid? chapterId, string first = "Alice", string last = "Applicant") =>
        new() {
            FirstName = first,
            LastName = last,
            DateOfBirth = new DateTime(1990, 1, 1),
            Citizenship = "DE",
            Email = "applicant@test.local",
            PhoneNumber = "0123456789",
            AddressStreet = "Teststr.",
            AddressHouseNbr = "1",
            AddressPostCode = "12345",
            AddressCity = "Testcity",
            ChapterId = chapterId,
            ConformityDeclarationAccepted = true,
            ApplicationText = "I want to join.",
            EntryDate = DateTime.UtcNow.Date
        };

    private async Task<HttpResponseMessage> Submit(MembershipApplicationDTO dto) {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync("/api/membershipapplications", dto);
        await ConfirmAllPendingSubmissionsAsync();
        return response;
    }

    [Test]
    public async Task Notifies_users_with_ProcessApplications_on_chapter() {
        var chapter = Builder.SeedChapter("C");
        var (intake, _) = Builder.SeedAuthenticatedUser(
            email: "intake@test.local",
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.ProcessApplications } });
        Builder.SeedAuthenticatedUser(email: "bystander@test.local");

        var response = await Submit(Dto(chapter.Id));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var logs = Db.NotificationLogs.Where(l => l.TriggerId == "application_submitted").ToList();
        await Assert.That(logs.Count).IsEqualTo(1);
        await Assert.That(logs[0].RecipientUserId).IsEqualTo(intake.Id);
        await Assert.That(logs[0].SourceEntityType).IsEqualTo("MembershipApplication");
        await Assert.That(logs[0].Subject!.Contains("Alice")).IsTrue();
    }

    [Test]
    public async Task Skips_dispatch_when_chapter_is_null() {
        Builder.SeedAuthenticatedUser(
            email: "global@test.local",
            globalPermissions: new[] { PermissionIdentifier.ProcessApplications });

        // Without a chapter, the application has no scope — no notification fires
        // (matches the existing motion-spawn branch which is also guarded by ChapterId).
        var response = await Submit(Dto(chapterId: null));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var logs = Db.NotificationLogs.Where(l => l.TriggerId == "application_submitted").ToList();
        await Assert.That(logs.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Global_ProcessApplications_holder_receives_notification() {
        var chapter = Builder.SeedChapter("C");
        var (admin, _) = Builder.SeedAuthenticatedUser(
            email: "admin@test.local",
            globalPermissions: new[] { PermissionIdentifier.ProcessApplications });

        await Submit(Dto(chapter.Id));

        var logs = Db.NotificationLogs.Where(l => l.TriggerId == "application_submitted").ToList();
        await Assert.That(logs.Count).IsEqualTo(1);
        await Assert.That(logs[0].RecipientUserId).IsEqualTo(admin.Id);
    }
}
