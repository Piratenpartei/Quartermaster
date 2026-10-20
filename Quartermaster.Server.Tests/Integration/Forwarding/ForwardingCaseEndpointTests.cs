using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Quartermaster.Api;
using Quartermaster.Api.Forwarding;
using Quartermaster.Data.Forwarding;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Forwarding;

public class ForwardingCaseEndpointTests : IntegrationTestBase {
    private ForwardingCase SeedCase(ForwardingCaseKind kind, ForwardingCaseStatus status = ForwardingCaseStatus.Failed) {
        var chapter = Builder.SeedChapter("Bund");
        var forwardingCase = new ForwardingCase { Kind = kind, Status = status, PayloadJson = "{\"secret\":\"IBAN-DE00\"}", CreatedAt = DateTime.UtcNow, DecidingChapterId = chapter.Id };
        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ForwardingCaseRepository>().Create(forwardingCase, [
            new ForwardingStep { Order = 1, Kind = ForwardingStepKind.CreateTicket, TargetChapterId = chapter.Id, Purpose = ForwardingPurpose.Application, State = ForwardingStepState.Failed, Attempts = 3, LastError = "Redmine ist nicht erreichbar.", UpdatedAt = DateTime.UtcNow }
        ]);
        return forwardingCase;
    }

    private ForwardingCase Reload(Guid id) {
        return Db.ForwardingCases.Single(c => c.Id == id);
    }

    [Test]
    public async Task List_filters_by_permission() {
        var application = SeedCase(ForwardingCaseKind.Application);
        var dues = SeedCase(ForwardingCaseKind.DueSelection);
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: [PermissionIdentifier.ProcessApplications]);
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.GetAsync("/api/forwarding/cases?Status=Failed");
        var body = await response.Content.ReadAsStringAsync();
        var list = (await response.Content.ReadFromJsonAsync<ForwardingCaseListResponse>())!.Items;

        await Assert.That(list.Select(c => c.Id).ToArray()).IsEquivalentTo(new[] { application.Id });
        await Assert.That(list[0].Steps.Single().LastError).IsEqualTo("Redmine ist nicht erreichbar.");
        await Assert.That(body).DoesNotContain("IBAN-DE00");
        await Assert.That(list.Any(c => c.Id == dues.Id)).IsFalse();
    }

    [Test]
    public async Task List_without_permission_is_403() {
        SeedCase(ForwardingCaseKind.Application);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.GetAsync("/api/forwarding/cases");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Retry_requires_matching_permission() {
        var dues = SeedCase(ForwardingCaseKind.DueSelection);
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: [PermissionIdentifier.ProcessApplications]);
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync($"/api/forwarding/cases/{dues.Id}/retry", new { });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(Reload(dues.Id).Status).IsEqualTo(ForwardingCaseStatus.Failed);
    }

    [Test]
    public async Task Retry_requeues_failed_case() {
        var dues = SeedCase(ForwardingCaseKind.DueSelection);
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: [PermissionIdentifier.ProcessDueSelections]);
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync($"/api/forwarding/cases/{dues.Id}/retry", new { });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Reload(dues.Id).Status).IsEqualTo(ForwardingCaseStatus.Open);
        await Assert.That(Db.ForwardingSteps.Single(s => s.CaseId == dues.Id).State).IsEqualTo(ForwardingStepState.Pending);
    }

    [Test]
    public async Task Resolve_clears_payload() {
        var application = SeedCase(ForwardingCaseKind.Application);
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: [PermissionIdentifier.ProcessApplications]);
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync($"/api/forwarding/cases/{application.Id}/resolve", new { });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Reload(application.Id).PayloadJson).IsNull();
        await Assert.That(Reload(application.Id).Status).IsEqualTo(ForwardingCaseStatus.ManuallyResolved);
    }

    [Test]
    public async Task Anonymous_is_401() {
        var application = SeedCase(ForwardingCaseKind.Application);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);

        var list = await client.GetAsync("/api/forwarding/cases");
        var retry = await client.PostAsJsonAsync($"/api/forwarding/cases/{application.Id}/retry", new { });
        var resolve = await client.PostAsJsonAsync($"/api/forwarding/cases/{application.Id}/resolve", new { });

        await Assert.That(list.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(retry.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(resolve.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }
    [Test]
    public async Task List_pages_through_cases() {
        SeedCase(ForwardingCaseKind.Application);
        SeedCase(ForwardingCaseKind.Application);
        SeedCase(ForwardingCaseKind.Application);
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: [PermissionIdentifier.ProcessApplications]);
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.GetAsync("/api/forwarding/cases?Page=2&PageSize=2");
        var page = await response.Content.ReadFromJsonAsync<ForwardingCaseListResponse>();

        await Assert.That(page!.Items.Count).IsEqualTo(1);
        await Assert.That(page.TotalCount).IsEqualTo(3);
        await Assert.That(page.Page).IsEqualTo(2);
    }
}
