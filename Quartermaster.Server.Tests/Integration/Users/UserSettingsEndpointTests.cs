using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Users;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Users;

public class UserSettingsEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/users/settings");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_401_when_token_invalid() {
        using var client = AuthenticatedClient("not-a-valid-token");
        var response = await client.GetAsync("/api/users/settings");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_callers_own_info_without_special_permission() {
        var (user, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/users/settings");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<UserSettingsDTO>();
        await Assert.That(dto!.User.Id).IsEqualTo(user.Id);
        await Assert.That(dto.User.EMail).IsEqualTo(user.EMail);
    }

    [Test]
    public async Task Returns_global_permissions_with_display_names() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewUsers });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/users/settings");
        var dto = await response.Content.ReadFromJsonAsync<UserSettingsDTO>();
        await Assert.That(dto!.GlobalPermissions.Any(p => p.Identifier == PermissionIdentifier.ViewUsers)).IsTrue();
    }

    [Test]
    public async Task Includes_linked_member_info_when_user_has_member() {
        var chapter = Builder.SeedChapter("Hannover");
        var (user, token) = Builder.SeedAuthenticatedUser();
        Builder.SeedMember(chapterId: chapter.Id, memberNumber: 4242, userId: user.Id);
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/users/settings");
        var dto = await response.Content.ReadFromJsonAsync<UserSettingsDTO>();
        await Assert.That(dto!.Member).IsNotNull();
        await Assert.That(dto.Member!.MemberNumber).IsEqualTo(4242);
        await Assert.That(dto.Member.ChapterName).IsEqualTo("Hannover");
    }

    [Test]
    public async Task Member_info_is_null_when_no_member_linked() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/users/settings");
        var dto = await response.Content.ReadFromJsonAsync<UserSettingsDTO>();
        await Assert.That(dto!.Member).IsNull();
    }
}
