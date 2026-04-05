using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Users;

public class RevokeGlobalPermissionEndpointTests : IntegrationTestBase {
    private static HttpRequestMessage BuildDeleteWithBody(string url, object body) {
        var req = new HttpRequestMessage(HttpMethod.Delete, url);
        var json = JsonSerializer.Serialize(body);
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return req;
    }

    [Test]
    public async Task Returns_401_when_anonymous() {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var req = BuildDeleteWithBody(
            $"/api/users/{Guid.NewGuid()}/permissions/global",
            new { PermissionIdentifier = PermissionIdentifier.ViewUsers });
        var response = await client.SendAsync(req);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_create_user_permission() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewUsers });
        var target = Builder.SeedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var req = BuildDeleteWithBody(
            $"/api/users/{target.Id}/permissions/global",
            new { PermissionIdentifier = PermissionIdentifier.ViewUsers });
        var response = await client.SendAsync(req);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_404_when_permission_identifier_unknown() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.CreateUser });
        var target = Builder.SeedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var req = BuildDeleteWithBody(
            $"/api/users/{target.Id}/permissions/global",
            new { PermissionIdentifier = "nonexistent_permission_xyz" });
        var response = await client.SendAsync(req);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Revokes_permission_from_target_user() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.CreateUser });
        var target = Builder.SeedUser();
        Builder.GrantGlobalPermission(target.Id, PermissionIdentifier.ViewUsers);
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var req = BuildDeleteWithBody(
            $"/api/users/{target.Id}/permissions/global",
            new { PermissionIdentifier = PermissionIdentifier.ViewUsers });
        var response = await client.SendAsync(req);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var remaining = Db.UserGlobalPermissions.Any(p => p.UserId == target.Id);
        await Assert.That(remaining).IsFalse();
    }

    [Test]
    public async Task Returns_ok_when_user_did_not_have_permission() {
        // Idempotent revoke — permission isn't there anyway
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.CreateUser });
        Builder.SeedPermission(PermissionIdentifier.ViewUsers, "View Users", global: true);
        var target = Builder.SeedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var req = BuildDeleteWithBody(
            $"/api/users/{target.Id}/permissions/global",
            new { PermissionIdentifier = PermissionIdentifier.ViewUsers });
        var response = await client.SendAsync(req);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
