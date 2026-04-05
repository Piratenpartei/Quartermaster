using System;
using System.Net;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Roles;

/// <summary>
/// PENDING: Deleting a role assignment that does not exist should return 404.
/// Today it returns 200 OK (best-effort / idempotent delete). Either behavior can
/// be correct — but we should match the rest of the API's convention (404 for
/// missing entities on detail/update/delete). Fails today; will pass once the
/// endpoint checks existence before calling <c>RevokeAssignment</c>.
/// See: code-quality-todos.md "Endpoint behavior review".
/// </summary>
public class RoleAssignmentDeleteNotFoundPendingTests : IntegrationTestBase {
    [Test]
    public async Task Delete_nonexistent_assignment_should_return_404() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ManageRoles });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.DeleteAsync($"/api/roleassignments/{Guid.NewGuid()}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
