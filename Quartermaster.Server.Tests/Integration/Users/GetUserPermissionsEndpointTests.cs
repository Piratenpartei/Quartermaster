using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Users;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Users;

public class GetUserPermissionsEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/users/{Guid.NewGuid()}/permissions");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_view_users_permission() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        var target = Builder.SeedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/users/{target.Id}/permissions");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_permissions_for_target_user() {
        var chapter = Builder.SeedChapter("Berlin");
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewUsers });
        var target = Builder.SeedUser();
        Builder.GrantGlobalPermission(target.Id, PermissionIdentifier.CreateChapter);
        Builder.GrantChapterPermission(target.Id, chapter.Id, PermissionIdentifier.ViewEvents);
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/users/{target.Id}/permissions");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<UserPermissionsDTO>();
        await Assert.That(dto!.GlobalPermissions).Contains(PermissionIdentifier.CreateChapter);
        await Assert.That(dto.ChapterPermissions.ContainsKey(chapter.Id.ToString())).IsTrue();
        await Assert.That(dto.ChapterPermissions[chapter.Id.ToString()]).Contains(PermissionIdentifier.ViewEvents);
    }

    [Test]
    public async Task Returns_empty_lists_when_target_has_no_permissions() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewUsers });
        var target = Builder.SeedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/users/{target.Id}/permissions");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<UserPermissionsDTO>();
        await Assert.That(dto!.GlobalPermissions.Count).IsEqualTo(0);
        await Assert.That(dto.ChapterPermissions.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Returns_empty_lists_for_nonexistent_user_id() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewUsers });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/users/{Guid.NewGuid()}/permissions");
        // Endpoint does not 404 — it just returns empty lists.
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<UserPermissionsDTO>();
        await Assert.That(dto!.GlobalPermissions.Count).IsEqualTo(0);
    }
}
