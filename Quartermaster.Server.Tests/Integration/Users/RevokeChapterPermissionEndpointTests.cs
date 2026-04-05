using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Users;

public class RevokeChapterPermissionEndpointTests : IntegrationTestBase {
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
            $"/api/users/{Guid.NewGuid()}/permissions/chapter",
            new { ChapterId = Guid.NewGuid(), PermissionIdentifier = PermissionIdentifier.ViewEvents });
        var response = await client.SendAsync(req);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_create_user_permission() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewUsers });
        var target = Builder.SeedUser();
        var chapter = Builder.SeedChapter();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var req = BuildDeleteWithBody(
            $"/api/users/{target.Id}/permissions/chapter",
            new { ChapterId = chapter.Id, PermissionIdentifier = PermissionIdentifier.ViewEvents });
        var response = await client.SendAsync(req);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_404_when_permission_identifier_unknown() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.CreateUser });
        var target = Builder.SeedUser();
        var chapter = Builder.SeedChapter();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var req = BuildDeleteWithBody(
            $"/api/users/{target.Id}/permissions/chapter",
            new { ChapterId = chapter.Id, PermissionIdentifier = "nonexistent_perm" });
        var response = await client.SendAsync(req);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Revokes_chapter_permission_from_target() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.CreateUser });
        var target = Builder.SeedUser();
        var chapter = Builder.SeedChapter("Bremen");
        Builder.GrantChapterPermission(target.Id, chapter.Id, PermissionIdentifier.ViewEvents);
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var req = BuildDeleteWithBody(
            $"/api/users/{target.Id}/permissions/chapter",
            new { ChapterId = chapter.Id, PermissionIdentifier = PermissionIdentifier.ViewEvents });
        var response = await client.SendAsync(req);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var remaining = Db.UserChapterPermissions
            .Any(p => p.UserId == target.Id && p.ChapterId == chapter.Id);
        await Assert.That(remaining).IsFalse();
    }

    [Test]
    public async Task Returns_ok_when_user_did_not_have_permission() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.CreateUser });
        var target = Builder.SeedUser();
        var chapter = Builder.SeedChapter();
        Builder.SeedPermission(PermissionIdentifier.ViewEvents, "View Events", global: false);
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var req = BuildDeleteWithBody(
            $"/api/users/{target.Id}/permissions/chapter",
            new { ChapterId = chapter.Id, PermissionIdentifier = PermissionIdentifier.ViewEvents });
        var response = await client.SendAsync(req);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
