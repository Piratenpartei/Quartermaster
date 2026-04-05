using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Roles;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Roles;

public class RoleCreateEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync("/api/roles", new RoleCreateRequest {
            Name = "R",
            Scope = 0,
            Permissions = new List<string>()
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_manage_roles() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/roles", new RoleCreateRequest {
            Name = "R",
            Scope = 0,
            Permissions = new List<string>()
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Creates_global_role_with_global_permission() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ManageRoles });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/roles", new RoleCreateRequest {
            Name = "Global Editors",
            Description = "Test",
            Scope = 0,
            Permissions = new List<string> { PermissionIdentifier.ViewUsers }
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<RoleDTO>();
        await Assert.That(dto!.Name).IsEqualTo("Global Editors");
        await Assert.That(dto.Scope).IsEqualTo(0);
        await Assert.That(dto.IsSystem).IsFalse();
        var persisted = Db.Roles.Any(r => r.Id == dto.Id);
        await Assert.That(persisted).IsTrue();
    }

    [Test]
    public async Task Returns_400_when_name_empty() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ManageRoles });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/roles", new RoleCreateRequest {
            Name = "",
            Scope = 0,
            Permissions = new List<string>()
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Rejects_global_role_containing_chapter_permission() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ManageRoles });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/roles", new RoleCreateRequest {
            Name = "Bad",
            Scope = 0, // Global
            Permissions = new List<string> { PermissionIdentifier.ViewApplications } // chapter-scoped
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Rejects_chapter_role_containing_global_permission() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ManageRoles });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/roles", new RoleCreateRequest {
            Name = "Bad2",
            Scope = 1, // ChapterScoped
            Permissions = new List<string> { PermissionIdentifier.ViewUsers } // global
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Rejects_invalid_scope() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ManageRoles });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/roles", new RoleCreateRequest {
            Name = "X",
            Scope = 99,
            Permissions = new List<string>()
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}
