using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.AuditLog;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.AuditLog;

public class AuditLogEndpointTests : IntegrationTestBase {
    private void SeedAudit(string entityType, Guid entityId, string action = "Created") {
        Db.Insert(new Quartermaster.Data.AuditLog.AuditLog {
            Id = Guid.NewGuid(),
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Timestamp = DateTime.UtcNow
        });
    }

    [Test]
    public async Task Returns_401_when_anonymous() {
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/auditlog?EntityType=User&EntityId={Guid.NewGuid()}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_view_audit() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/auditlog?EntityType=User&EntityId={Guid.NewGuid()}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_empty_list_when_no_entries_match() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewAudit });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/auditlog?EntityType=User&EntityId={Guid.NewGuid()}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<List<AuditLogDTO>>();
        await Assert.That(list!.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Returns_audit_entries_for_matching_entity() {
        var entityId = Guid.NewGuid();
        SeedAudit("User", entityId, "Created");
        SeedAudit("User", entityId, "Updated");
        SeedAudit("User", Guid.NewGuid(), "Created");
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewAudit });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/auditlog?EntityType=User&EntityId={entityId}");
        var list = await response.Content.ReadFromJsonAsync<List<AuditLogDTO>>();
        await Assert.That(list!.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Does_not_return_entries_for_different_entity_type() {
        var entityId = Guid.NewGuid();
        SeedAudit("User", entityId);
        SeedAudit("Chapter", entityId);
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewAudit });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/auditlog?EntityType=User&EntityId={entityId}");
        var list = await response.Content.ReadFromJsonAsync<List<AuditLogDTO>>();
        await Assert.That(list!.Count).IsEqualTo(1);
        await Assert.That(list[0].EntityType).IsEqualTo("User");
    }
}
