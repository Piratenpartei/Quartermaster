using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Admin;

public class MembershipApplicationProcessEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        var chapter = Builder.SeedChapter("C");
        var app = Builder.SeedMembershipApplication(chapter.Id);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync("/api/admin/membershipapplications/process", new {
            Id = app.Id,
            Status = (int)ApplicationStatus.Approved
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_404_for_nonexistent_application() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ProcessApplications });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/admin/membershipapplications/process", new {
            Id = Guid.NewGuid(),
            Status = (int)ApplicationStatus.Approved
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_process_permission() {
        var chapter = Builder.SeedChapter("C");
        var app = Builder.SeedMembershipApplication(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewApplications });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/admin/membershipapplications/process", new {
            Id = app.Id,
            Status = (int)ApplicationStatus.Approved
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Approves_application_and_updates_status() {
        var chapter = Builder.SeedChapter("C");
        var app = Builder.SeedMembershipApplication(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ProcessApplications });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/admin/membershipapplications/process", new {
            Id = app.Id,
            Status = (int)ApplicationStatus.Approved
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updated = Db.MembershipApplications.First(a => a.Id == app.Id);
        await Assert.That(updated.Status).IsEqualTo(ApplicationStatus.Approved);
        await Assert.That(updated.ProcessedAt).IsNotNull();
    }

    [Test]
    public async Task Rejects_invalid_target_status_Pending() {
        var chapter = Builder.SeedChapter("C");
        var app = Builder.SeedMembershipApplication(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ProcessApplications });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/admin/membershipapplications/process", new {
            Id = app.Id,
            Status = (int)ApplicationStatus.Pending
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Chapter_processor_can_approve_own_chapter_application() {
        var chapter = Builder.SeedChapter("C");
        var app = Builder.SeedMembershipApplication(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.ProcessApplications } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/admin/membershipapplications/process", new {
            Id = app.Id,
            Status = (int)ApplicationStatus.Rejected
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
