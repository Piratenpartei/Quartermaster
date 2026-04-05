using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Data.Roles;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Roles;

public class RoleAssignmentDeleteEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.DeleteAsync($"/api/roleassignments/{Guid.NewGuid()}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_manage_roles() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.DeleteAsync($"/api/roleassignments/{Guid.NewGuid()}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Removes_existing_assignment() {
        var role = Builder.SeedRole("r", "R", RoleScope.Global);
        var target = Builder.SeedUser();
        Builder.AssignRoleToUser(target.Id, role.Id);
        var assignmentId = Db.UserRoleAssignments.First(a => a.UserId == target.Id).Id;

        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ManageRoles });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.DeleteAsync($"/api/roleassignments/{assignmentId}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var stillExists = Db.UserRoleAssignments.Any(a => a.Id == assignmentId);
        await Assert.That(stillExists).IsFalse();
    }

    [Test]
    public async Task Returns_OK_for_nonexistent_assignment() {
        // Endpoint does a best-effort delete and returns OK either way.
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ManageRoles });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.DeleteAsync($"/api/roleassignments/{Guid.NewGuid()}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Does_not_affect_other_assignments() {
        var role = Builder.SeedRole("r", "R", RoleScope.Global);
        var u1 = Builder.SeedUser();
        var u2 = Builder.SeedUser();
        Builder.AssignRoleToUser(u1.Id, role.Id);
        Builder.AssignRoleToUser(u2.Id, role.Id);
        var toDelete = Db.UserRoleAssignments.First(a => a.UserId == u1.Id).Id;

        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ManageRoles });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.DeleteAsync($"/api/roleassignments/{toDelete}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var remaining = Db.UserRoleAssignments.Count(a => a.RoleId == role.Id);
        await Assert.That(remaining).IsEqualTo(1);
    }
}
