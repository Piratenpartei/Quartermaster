using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Users;

public class UserDeleteEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.DeleteAsync($"/api/users/{Guid.NewGuid()}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_delete_permission() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        var target = Builder.SeedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.DeleteAsync($"/api/users/{target.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_400_when_deleting_self() {
        var (user, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.DeleteUsers });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.DeleteAsync($"/api/users/{user.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Returns_404_for_nonexistent_id() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.DeleteUsers });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.DeleteAsync($"/api/users/{Guid.NewGuid()}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Soft_deletes_target_user() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.DeleteUsers });
        var target = Builder.SeedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.DeleteAsync($"/api/users/{target.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var reloaded = Db.Users.FirstOrDefault(u => u.Id == target.Id);
        await Assert.That(reloaded).IsNotNull();
        await Assert.That(reloaded!.DeletedAt).IsNotNull();
    }

    [Test]
    public async Task Invalidates_all_tokens_for_deleted_user() {
        var (_, adminToken) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.DeleteUsers });
        var target = Builder.SeedUser();
        var targetToken = Builder.SeedLoginToken(target.Id);
        // Sanity — target token should work before deletion
        using (var targetClient = AuthenticatedClient(targetToken)) {
            var preResp = await targetClient.GetAsync("/api/users/session");
            await Assert.That(preResp.StatusCode).IsEqualTo(HttpStatusCode.OK);
        }
        using var adminClient = await AuthenticatedClientWithCsrfAsync(adminToken);
        var response = await adminClient.DeleteAsync($"/api/users/{target.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        using (var targetClient = AuthenticatedClient(targetToken)) {
            var postResp = await targetClient.GetAsync("/api/users/session");
            await Assert.That(postResp.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        }
    }
}
