using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Users;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Users;

public class SessionEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/users/session");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_401_when_token_invalid() {
        using var client = AuthenticatedClient("invalid-token-xyz");
        var response = await client.GetAsync("/api/users/session");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_user_info_when_authenticated() {
        var (user, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/users/session");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<LoginResponse>();
        await Assert.That(dto).IsNotNull();
        await Assert.That(dto!.User.Id).IsEqualTo(user.Id);
        await Assert.That(dto.User.EMail).IsEqualTo(user.EMail);
    }

    [Test]
    public async Task Returns_global_permissions() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewUsers, PermissionIdentifier.CreateChapter });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/users/session");
        var dto = await response.Content.ReadFromJsonAsync<LoginResponse>();
        await Assert.That(dto!.Permissions.Global).Contains(PermissionIdentifier.ViewUsers);
        await Assert.That(dto.Permissions.Global).Contains(PermissionIdentifier.CreateChapter);
    }

    [Test]
    public async Task Returns_chapter_permissions() {
        var chapter = Builder.SeedChapter("Test Chapter");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new System.Collections.Generic.Dictionary<Guid, string[]> {
                { chapter.Id, new[] { PermissionIdentifier.ViewEvents } }
            });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/users/session");
        var dto = await response.Content.ReadFromJsonAsync<LoginResponse>();
        await Assert.That(dto!.Permissions.Chapters.ContainsKey(chapter.Id.ToString())).IsTrue();
        await Assert.That(dto.Permissions.Chapters[chapter.Id.ToString()]).Contains(PermissionIdentifier.ViewEvents);
    }

    [Test]
    public async Task Builds_display_name_from_first_and_last_name() {
        var (user, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/users/session");
        var dto = await response.Content.ReadFromJsonAsync<LoginResponse>();
        await Assert.That(dto!.User.DisplayName).IsEqualTo($"{user.FirstName} {user.LastName}");
    }
}
