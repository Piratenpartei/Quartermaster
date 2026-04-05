using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Users;

public class GrantChapterPermissionEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync($"/api/users/{Guid.NewGuid()}/permissions/chapter",
            new { ChapterId = Guid.NewGuid(), PermissionIdentifier = PermissionIdentifier.ViewEvents });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_create_user_permission() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewUsers });
        var target = Builder.SeedUser();
        var chapter = Builder.SeedChapter();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync($"/api/users/{target.Id}/permissions/chapter",
            new { ChapterId = chapter.Id, PermissionIdentifier = PermissionIdentifier.ViewEvents });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_404_when_permission_identifier_unknown() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.CreateUser });
        var target = Builder.SeedUser();
        var chapter = Builder.SeedChapter();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync($"/api/users/{target.Id}/permissions/chapter",
            new { ChapterId = chapter.Id, PermissionIdentifier = "nonexistent_perm" });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Grants_chapter_permission_to_target() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.CreateUser });
        var target = Builder.SeedUser();
        var chapter = Builder.SeedChapter("Hamburg");
        Builder.SeedPermission(PermissionIdentifier.ViewEvents, "View Events", global: false);
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync($"/api/users/{target.Id}/permissions/chapter",
            new { ChapterId = chapter.Id, PermissionIdentifier = PermissionIdentifier.ViewEvents });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var granted = Db.UserChapterPermissions
            .Any(p => p.UserId == target.Id && p.ChapterId == chapter.Id);
        await Assert.That(granted).IsTrue();
    }
}
