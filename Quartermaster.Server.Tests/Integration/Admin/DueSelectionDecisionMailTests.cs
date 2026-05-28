using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.DueSelector;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Admin;

public class DueSelectionDecisionMailTests : IntegrationTestBase {
    private Guid SeedDueSelectionWithEmail(string email) {
        var ds = Builder.SeedDueSelection();
        Db.DueSelections.Where(d => d.Id == ds.Id).Set(d => d.Email, (string?)email).Update();
        return ds.Id;
    }

    [Test]
    public async Task Approving_due_selection_sends_approved_mail() {
        var id = SeedDueSelectionWithEmail("due@test.local");
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ProcessDueSelections });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/admin/dueselections/process", new {
            Id = id,
            Status = DueSelectionStatus.Approved
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var logs = Db.NotificationLogs.Where(l => l.TriggerId == "dueselection_approved").ToList();
        await Assert.That(logs.Count).IsEqualTo(1);
        await Assert.That(logs[0].Recipient).IsEqualTo("due@test.local");
        await Assert.That(logs[0].SourceEntityType).IsEqualTo("DueSelection");
    }

    [Test]
    public async Task Rejecting_due_selection_sends_rejected_mail() {
        var id = SeedDueSelectionWithEmail("due@test.local");
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ProcessDueSelections });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/admin/dueselections/process", new {
            Id = id,
            Status = DueSelectionStatus.Rejected
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var logs = Db.NotificationLogs.Where(l => l.TriggerId == "dueselection_rejected").ToList();
        await Assert.That(logs.Count).IsEqualTo(1);
        await Assert.That(logs[0].Recipient).IsEqualTo("due@test.local");
    }
}
