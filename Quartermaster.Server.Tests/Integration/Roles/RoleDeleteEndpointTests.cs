using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Data.Roles;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Roles;

public class RoleDeleteEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        var role = Builder.SeedRole("r", "R", RoleScope.Global);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.DeleteAsync($"/api/roles/{role.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_manage_roles() {
        var role = Builder.SeedRole("r", "R", RoleScope.Global);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.DeleteAsync($"/api/roles/{role.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_404_for_nonexistent_role() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ManageRoles });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.DeleteAsync($"/api/roles/{Guid.NewGuid()}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Deletes_custom_role() {
        var role = Builder.SeedRole("custom", "X", RoleScope.Global);
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ManageRoles });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.DeleteAsync($"/api/roles/{role.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var exists = Db.Roles.Any(r => r.Id == role.Id);
        await Assert.That(exists).IsFalse();
    }

    [Test]
    public async Task Rejects_delete_of_system_role() {
        var sysRole = Builder.SeedRole("sys", "System", RoleScope.Global, isSystem: true);
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ManageRoles });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.DeleteAsync($"/api/roles/{sysRole.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var stillExists = Db.Roles.Any(r => r.Id == sysRole.Id);
        await Assert.That(stillExists).IsTrue();
    }
}
