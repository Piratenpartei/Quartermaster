using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Roles;
using Quartermaster.Data.Roles;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Roles;

public class RoleAssignmentCreateEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync("/api/roleassignments", new RoleAssignmentCreateRequest {
            UserId = Guid.NewGuid(),
            RoleId = Guid.NewGuid(),
            ChapterId = null
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_manage_roles() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/roleassignments", new RoleAssignmentCreateRequest {
            UserId = Guid.NewGuid(),
            RoleId = Guid.NewGuid()
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Assigns_global_role_with_null_chapter() {
        var role = Builder.SeedRole("gr", "Global", RoleScope.Global);
        var target = Builder.SeedUser();
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ManageRoles });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/roleassignments", new RoleAssignmentCreateRequest {
            UserId = target.Id,
            RoleId = role.Id,
            ChapterId = null
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var assigned = Db.UserRoleAssignments.Any(a => a.UserId == target.Id && a.RoleId == role.Id);
        await Assert.That(assigned).IsTrue();
    }

    [Test]
    public async Task Rejects_global_role_with_chapter_id() {
        var role = Builder.SeedRole("gr", "Global", RoleScope.Global);
        var chapter = Builder.SeedChapter("C");
        var target = Builder.SeedUser();
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ManageRoles });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/roleassignments", new RoleAssignmentCreateRequest {
            UserId = target.Id,
            RoleId = role.Id,
            ChapterId = chapter.Id
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Rejects_chapter_role_without_chapter_id() {
        var role = Builder.SeedRole("cr", "Ch", RoleScope.ChapterScoped);
        var target = Builder.SeedUser();
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ManageRoles });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/roleassignments", new RoleAssignmentCreateRequest {
            UserId = target.Id,
            RoleId = role.Id,
            ChapterId = null
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Rejects_nonexistent_role() {
        var target = Builder.SeedUser();
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ManageRoles });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/roleassignments", new RoleAssignmentCreateRequest {
            UserId = target.Id,
            RoleId = Guid.NewGuid(),
            ChapterId = null
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Assigns_chapter_role_with_valid_chapter() {
        var role = Builder.SeedRole("cr", "Ch", RoleScope.ChapterScoped);
        var chapter = Builder.SeedChapter("C");
        var target = Builder.SeedUser();
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ManageRoles });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/roleassignments", new RoleAssignmentCreateRequest {
            UserId = target.Id,
            RoleId = role.Id,
            ChapterId = chapter.Id
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
