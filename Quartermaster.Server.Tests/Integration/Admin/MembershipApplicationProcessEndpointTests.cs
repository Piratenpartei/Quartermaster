using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Data.Forwarding;
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
            Status = ApplicationStatus.Approved
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
            Status = ApplicationStatus.Approved
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
            Status = ApplicationStatus.Approved
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
            Status = ApplicationStatus.Approved
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
            Status = ApplicationStatus.Pending
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
            Status = ApplicationStatus.Rejected
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
    [Test]
    public async Task Decided_application_cannot_be_processed_again() {
        var chapter = Builder.SeedChapter("C");
        var app = Builder.SeedMembershipApplication(chapter.Id);
        Db.MembershipApplications.Where(a => a.Id == app.Id).Set(a => a.Status, ApplicationStatus.Approved).Update();
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.ProcessApplications });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/admin/membershipapplications/process", new { Id = app.Id, Status = ApplicationStatus.Rejected });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains("error.admin.application.already_decided");
        await Assert.That(Db.MembershipApplications.Single(a => a.Id == app.Id).Status).IsEqualTo(ApplicationStatus.Approved);
    }

    [Test]
    public async Task Application_without_chapter_cannot_be_approved() {
        var app = Builder.SeedMembershipApplication(status: ApplicationStatus.PendingDivisionLinking);
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.ProcessApplications });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/admin/membershipapplications/process", new { Id = app.Id, Status = ApplicationStatus.Approved });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains("error.admin.application.chapter_unknown");
        await Assert.That(Db.MembershipApplications.Single(a => a.Id == app.Id).Status).IsEqualTo(ApplicationStatus.PendingDivisionLinking);
    }

    [Test]
    public async Task Application_without_chapter_can_be_rejected() {
        var app = Builder.SeedMembershipApplication(status: ApplicationStatus.PendingDivisionLinking);
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.ProcessApplications });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/admin/membershipapplications/process", new { Id = app.Id, Status = ApplicationStatus.Rejected });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.MembershipApplications.Single(a => a.Id == app.Id).Status).IsEqualTo(ApplicationStatus.Rejected);
    }

    private void SeedSageCase(Guid chapterId, Guid applicationId) {
        new ForwardingCaseRepository(Db).Create(
            new ForwardingCase { Kind = ForwardingCaseKind.Application, MembershipApplicationId = applicationId, CreatedAt = DateTime.UtcNow },
            [
                new ForwardingStep { Order = 1, Kind = ForwardingStepKind.CreateTicket, TargetChapterId = chapterId, Purpose = ForwardingPurpose.Application, State = ForwardingStepState.Done, UpdatedAt = DateTime.UtcNow },
                new ForwardingStep { Order = 2, Kind = ForwardingStepKind.AwaitSageEntry, TargetChapterId = chapterId, Purpose = ForwardingPurpose.Application, State = ForwardingStepState.Waiting, UpdatedAt = DateTime.UtcNow }
            ]);
    }

    [Test]
    public async Task Approval_with_sage_step_sends_no_mail() {
        var chapter = Builder.SeedChapter("C");
        var app = Builder.SeedMembershipApplication(chapter.Id);
        SeedSageCase(chapter.Id, app.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.ProcessApplications });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/admin/membershipapplications/process", new { Id = app.Id, Status = ApplicationStatus.Approved });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.MembershipApplications.Single(a => a.Id == app.Id).Status).IsEqualTo(ApplicationStatus.Approved);
        await Assert.That(Db.NotificationLogs.Count(l => l.TriggerId == "application_approved")).IsEqualTo(0);
    }

    [Test]
    public async Task Rejection_mails_at_once() {
        var chapter = Builder.SeedChapter("C");
        var app = Builder.SeedMembershipApplication(chapter.Id);
        SeedSageCase(chapter.Id, app.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.ProcessApplications });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/admin/membershipapplications/process", new { Id = app.Id, Status = ApplicationStatus.Rejected });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.NotificationLogs.Count(l => l.TriggerId == "application_rejected")).IsEqualTo(1);
    }
}
