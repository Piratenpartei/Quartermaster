using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Users;

public class GrantGlobalPermissionEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync($"/api/users/{Guid.NewGuid()}/permissions/global",
            new { PermissionIdentifier = PermissionIdentifier.ViewUsers });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_create_user_permission() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewUsers });
        var target = Builder.SeedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync($"/api/users/{target.Id}/permissions/global",
            new { PermissionIdentifier = PermissionIdentifier.ViewUsers });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_404_when_permission_identifier_unknown() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.CreateUser });
        var target = Builder.SeedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync($"/api/users/{target.Id}/permissions/global",
            new { PermissionIdentifier = "nonexistent_permission_xyz" });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Grants_permission_to_target_user() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.CreateUser });
        // Seed permission record so lookup succeeds
        Builder.SeedPermission(PermissionIdentifier.ViewUsers, "View Users", global: true);
        var target = Builder.SeedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync($"/api/users/{target.Id}/permissions/global",
            new { PermissionIdentifier = PermissionIdentifier.ViewUsers });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var granted = Db.UserGlobalPermissions
            .Any(p => p.UserId == target.Id);
        await Assert.That(granted).IsTrue();
    }

    [Test]
    public async Task Returns_403_for_unauthenticated_but_otherwise_valid_request() {
        // Second sanity check — caller has ViewUsers but not CreateUser
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.DeleteUsers });
        var target = Builder.SeedUser();
        Builder.SeedPermission(PermissionIdentifier.ViewUsers, "View Users", global: true);
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync($"/api/users/{target.Id}/permissions/global",
            new { PermissionIdentifier = PermissionIdentifier.ViewUsers });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }
}
