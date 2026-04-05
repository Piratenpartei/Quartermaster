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

public class RoleListEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/roles");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_manage_roles() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/roles");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_list_including_system_roles() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ManageRoles });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/roles");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var roles = await response.Content.ReadFromJsonAsync<List<RoleDTO>>();
        await Assert.That(roles).IsNotNull();
        // System role "chapter_officer" is seeded at startup
        await Assert.That(roles!.Any(r => r.IsSystem)).IsTrue();
    }

    [Test]
    public async Task Custom_role_included_in_list() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ManageRoles });
        Builder.SeedRole("custom_a", "Custom Role A", RoleScope.Global);
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/roles");
        var roles = await response.Content.ReadFromJsonAsync<List<RoleDTO>>();
        await Assert.That(roles!.Any(r => r.Name == "Custom Role A" && !r.IsSystem)).IsTrue();
    }

    [Test]
    public async Task Role_permissions_included_in_response() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ManageRoles });
        var role = Builder.SeedRole("custom_b", "Custom B", RoleScope.Global);
        Builder.AddPermissionToRole(role.Id, PermissionIdentifier.ViewUsers);
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/roles");
        var roles = await response.Content.ReadFromJsonAsync<List<RoleDTO>>();
        var customB = roles!.First(r => r.Name == "Custom B");
        await Assert.That(customB.Permissions).Contains(PermissionIdentifier.ViewUsers);
    }
}
