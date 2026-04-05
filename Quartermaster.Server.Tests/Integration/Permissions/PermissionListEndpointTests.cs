using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Permissions;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Permissions;

public class PermissionListEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/permissions");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_view_users_permission() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/permissions");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_200_with_permissions_when_authorized() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewUsers });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/permissions");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dtos = await response.Content.ReadFromJsonAsync<List<PermissionDTO>>();
        await Assert.That(dtos).IsNotNull();
        // At least the users_view permission we granted must be present
        await Assert.That(dtos!.Any(d => d.Identifier == PermissionIdentifier.ViewUsers)).IsTrue();
    }

    [Test]
    public async Task Includes_seeded_permission_identifiers() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewUsers });
        Builder.SeedPermission("test.perm.alpha", "Alpha", global: true);
        Builder.SeedPermission("test.perm.beta", "Beta", global: false);
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/permissions");
        var dtos = await response.Content.ReadFromJsonAsync<List<PermissionDTO>>();
        await Assert.That(dtos!.Any(d => d.Identifier == "test.perm.alpha" && d.Global)).IsTrue();
        await Assert.That(dtos.Any(d => d.Identifier == "test.perm.beta" && !d.Global)).IsTrue();
    }

    [Test]
    public async Task Includes_display_names() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewUsers });
        Builder.SeedPermission("test.perm.gamma", "Gamma Display", global: true);
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/permissions");
        var dtos = await response.Content.ReadFromJsonAsync<List<PermissionDTO>>();
        await Assert.That(dtos).IsNotNull();
        var gamma = dtos!.FirstOrDefault(d => d.Identifier == "test.perm.gamma");
        await Assert.That(gamma).IsNotNull();
        await Assert.That(gamma!.DisplayName).IsEqualTo("Gamma Display");
    }
}
