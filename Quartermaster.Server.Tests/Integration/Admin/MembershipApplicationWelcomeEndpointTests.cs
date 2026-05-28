using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Admin;

public class MembershipApplicationWelcomeEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        var chapter = Builder.SeedChapter("C");
        var app = Builder.SeedMembershipApplication(chapter.Id, status: ApplicationStatus.Approved);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync("/api/admin/membershipapplications/welcome", new {
            Id = app.Id,
            MemberNumber = 4711
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_404_for_nonexistent_application() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ProcessApplications });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/admin/membershipapplications/welcome", new {
            Id = Guid.NewGuid(),
            MemberNumber = 4711
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_process_permission() {
        var chapter = Builder.SeedChapter("C");
        var app = Builder.SeedMembershipApplication(chapter.Id, status: ApplicationStatus.Approved);
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewApplications });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/admin/membershipapplications/welcome", new {
            Id = app.Id,
            MemberNumber = 4711
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Rejects_when_application_not_approved() {
        var chapter = Builder.SeedChapter("C");
        var app = Builder.SeedMembershipApplication(chapter.Id, status: ApplicationStatus.Pending);
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ProcessApplications });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/admin/membershipapplications/welcome", new {
            Id = app.Id,
            MemberNumber = 4711
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var unchanged = Db.MembershipApplications.First(a => a.Id == app.Id);
        await Assert.That(unchanged.MemberNumber).IsNull();
    }

    [Test]
    public async Task Rejects_non_positive_member_number() {
        var chapter = Builder.SeedChapter("C");
        var app = Builder.SeedMembershipApplication(chapter.Id, status: ApplicationStatus.Approved);
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ProcessApplications });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/admin/membershipapplications/welcome", new {
            Id = app.Id,
            MemberNumber = 0
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Sets_member_number_and_sends_welcome_mail() {
        var chapter = Builder.SeedChapter("C");
        var app = Builder.SeedMembershipApplication(
            chapter.Id, email: "newmember@test.local", status: ApplicationStatus.Approved);
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ProcessApplications });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/admin/membershipapplications/welcome", new {
            Id = app.Id,
            MemberNumber = 4711
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var updated = Db.MembershipApplications.First(a => a.Id == app.Id);
        await Assert.That(updated.MemberNumber).IsEqualTo(4711);
        await Assert.That(updated.WelcomeSentAt).IsNotNull();

        var logs = Db.NotificationLogs.Where(l => l.TriggerId == "member_welcome").ToList();
        await Assert.That(logs.Count).IsEqualTo(1);
        await Assert.That(logs[0].Recipient).IsEqualTo("newmember@test.local");
        await Assert.That(logs[0].Body!.Contains("4711")).IsTrue();
    }

    [Test]
    public async Task Second_welcome_for_same_application_is_rejected() {
        var chapter = Builder.SeedChapter("C");
        var app = Builder.SeedMembershipApplication(chapter.Id, status: ApplicationStatus.Approved);
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ProcessApplications });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var first = await client.PostAsJsonAsync("/api/admin/membershipapplications/welcome", new {
            Id = app.Id,
            MemberNumber = 4711
        });
        await Assert.That(first.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var second = await client.PostAsJsonAsync("/api/admin/membershipapplications/welcome", new {
            Id = app.Id,
            MemberNumber = 9999
        });
        await Assert.That(second.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        var logs = Db.NotificationLogs.Where(l => l.TriggerId == "member_welcome").ToList();
        await Assert.That(logs.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Chapter_processor_can_welcome_own_chapter_applicant() {
        var chapter = Builder.SeedChapter("C");
        var app = Builder.SeedMembershipApplication(chapter.Id, status: ApplicationStatus.Approved);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.ProcessApplications } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/admin/membershipapplications/welcome", new {
            Id = app.Id,
            MemberNumber = 4711
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
