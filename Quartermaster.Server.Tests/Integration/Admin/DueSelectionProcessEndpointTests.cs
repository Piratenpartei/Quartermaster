using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Data.DueSelector;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Admin;

public class DueSelectionProcessEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        var due = Builder.SeedDueSelection();
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync("/api/admin/dueselections/process", new {
            Id = due.Id,
            Status = (int)DueSelectionStatus.Approved
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_404_for_nonexistent_selection() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ProcessDueSelections });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/admin/dueselections/process", new {
            Id = Guid.NewGuid(),
            Status = (int)DueSelectionStatus.Approved
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_process_permission() {
        var due = Builder.SeedDueSelection();
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewDueSelections });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/admin/dueselections/process", new {
            Id = due.Id,
            Status = (int)DueSelectionStatus.Approved
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Approves_selection_successfully() {
        var due = Builder.SeedDueSelection();
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ProcessDueSelections });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/admin/dueselections/process", new {
            Id = due.Id,
            Status = (int)DueSelectionStatus.Approved
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updated = Db.DueSelections.First(d => d.Id == due.Id);
        await Assert.That(updated.Status).IsEqualTo(DueSelectionStatus.Approved);
        await Assert.That(updated.ProcessedAt).IsNotNull();
    }

    [Test]
    public async Task Rejects_invalid_target_status_Pending() {
        var due = Builder.SeedDueSelection();
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ProcessDueSelections });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/admin/dueselections/process", new {
            Id = due.Id,
            Status = (int)DueSelectionStatus.Pending
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Rejects_selection_successfully() {
        var due = Builder.SeedDueSelection();
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ProcessDueSelections });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/admin/dueselections/process", new {
            Id = due.Id,
            Status = (int)DueSelectionStatus.Rejected
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updated = Db.DueSelections.First(d => d.Id == due.Id);
        await Assert.That(updated.Status).IsEqualTo(DueSelectionStatus.Rejected);
    }
}
