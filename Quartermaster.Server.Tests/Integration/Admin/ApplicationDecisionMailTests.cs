using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Admin;

public class ApplicationDecisionMailTests : IntegrationTestBase {
    [Test]
    public async Task Approving_application_sends_approved_mail() {
        var chapter = Builder.SeedChapter("C");
        var app = Builder.SeedMembershipApplication(chapter.Id, email: "applicant@test.local");
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ProcessApplications });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/admin/membershipapplications/process", new {
            Id = app.Id,
            Status = ApplicationStatus.Approved
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var logs = Db.NotificationLogs.Where(l => l.TriggerId == "application_approved").ToList();
        await Assert.That(logs.Count).IsEqualTo(1);
        await Assert.That(logs[0].Recipient).IsEqualTo("applicant@test.local");
        await Assert.That(logs[0].SourceEntityType).IsEqualTo("MembershipApplication");
    }

    [Test]
    public async Task Rejecting_application_sends_rejected_mail() {
        var chapter = Builder.SeedChapter("C");
        var app = Builder.SeedMembershipApplication(chapter.Id, email: "applicant@test.local");
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ProcessApplications });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/admin/membershipapplications/process", new {
            Id = app.Id,
            Status = ApplicationStatus.Rejected
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var logs = Db.NotificationLogs.Where(l => l.TriggerId == "application_rejected").ToList();
        await Assert.That(logs.Count).IsEqualTo(1);
        await Assert.That(logs[0].Recipient).IsEqualTo("applicant@test.local");
        await Assert.That(Db.NotificationLogs.Count(l => l.TriggerId == "application_approved")).IsEqualTo(0);
    }
}
