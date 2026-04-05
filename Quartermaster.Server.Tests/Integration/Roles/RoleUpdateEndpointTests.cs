using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Roles;
using Quartermaster.Data.Roles;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Roles;

public class RoleUpdateEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        var role = Builder.SeedRole("r", "R", RoleScope.Global);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PutAsJsonAsync($"/api/roles/{role.Id}", new RoleUpdateRequest {
            Id = role.Id,
            Name = "Updated",
            Permissions = new List<string>()
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_manage_roles() {
        var role = Builder.SeedRole("r", "R", RoleScope.Global);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/roles/{role.Id}", new RoleUpdateRequest {
            Id = role.Id,
            Name = "Updated",
            Permissions = new List<string>()
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_404_for_nonexistent_role() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ManageRoles });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var id = Guid.NewGuid();
        var response = await client.PutAsJsonAsync($"/api/roles/{id}", new RoleUpdateRequest {
            Id = id,
            Name = "X",
            Permissions = new List<string>()
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Updates_custom_role_name_and_description() {
        var role = Builder.SeedRole("custom", "Old", RoleScope.Global);
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ManageRoles });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/roles/{role.Id}", new RoleUpdateRequest {
            Id = role.Id,
            Name = "New Name",
            Description = "New Description",
            Permissions = new List<string>()
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updated = Db.Roles.First(r => r.Id == role.Id);
        await Assert.That(updated.Name).IsEqualTo("New Name");
        await Assert.That(updated.Description).IsEqualTo("New Description");
    }

    [Test]
    public async Task Rejects_updates_to_system_roles() {
        var sysRole = Builder.SeedRole("sys", "System", RoleScope.Global, isSystem: true);
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ManageRoles });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/roles/{sysRole.Id}", new RoleUpdateRequest {
            Id = sysRole.Id,
            Name = "Tampered",
            Permissions = new List<string>()
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Returns_400_when_name_empty() {
        var role = Builder.SeedRole("custom", "Old", RoleScope.Global);
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ManageRoles });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/roles/{role.Id}", new RoleUpdateRequest {
            Id = role.Id,
            Name = "",
            Permissions = new List<string>()
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Rejects_permissions_mismatching_scope() {
        var role = Builder.SeedRole("custom", "X", RoleScope.Global);
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ManageRoles });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/roles/{role.Id}", new RoleUpdateRequest {
            Id = role.Id,
            Name = "X",
            Permissions = new List<string> { PermissionIdentifier.ViewApplications } // chapter-scoped
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}
