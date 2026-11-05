using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
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
        await Assert.That(dto.User.Email).IsEqualTo(user.Email);
    }

    private async Task<HttpResponseMessage> ChooseDisplayName(string token, DisplayNameMode mode) {
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        return await client.PutAsJsonAsync("/api/users/settings/display-name", new DisplayNameUpdateRequest { Mode = mode });
    }

    private async Task<UserSettingsDTO> SettingsOf(string token) {
        using var client = AuthenticatedClient(token);
        return (await client.GetFromJsonAsync<UserSettingsDTO>("/api/users/settings"))!;
    }

    [Test]
    public async Task The_username_becomes_the_display_name_when_chosen() {
        var (user, token) = Builder.SeedAuthenticatedUser(firstName: "Erika", lastName: "Mustermann");
        Db.Users.Where(u => u.Id == user.Id).Set(u => u.Username, "erika.mustermann").Update();

        var chosen = await ChooseDisplayName(token, DisplayNameMode.Username);
        var settings = await SettingsOf(token);
        var reset = await ChooseDisplayName(token, DisplayNameMode.RealName);

        await Assert.That(chosen.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(settings.User.DisplayName).IsEqualTo("erika.mustermann");
        await Assert.That(settings.DisplayNameMode).IsEqualTo(DisplayNameMode.Username);
        await Assert.That(reset.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That((await SettingsOf(token)).User.DisplayName).IsEqualTo("Erika Mustermann");
    }

    [Test]
    public async Task The_username_cannot_be_chosen_before_the_sso_delivered_one() {
        var (user, token) = Builder.SeedAuthenticatedUser(firstName: "Erika", lastName: "Mustermann");
        Db.Users.Where(u => u.Id == user.Id).Set(u => u.Username, user.Email).Update();

        var response = await ChooseDisplayName(token, DisplayNameMode.Username);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains(I18nKey.Error.User.DisplayNameUsernameMissing);
        await Assert.That((await SettingsOf(token)).CanUseUsername).IsFalse();
    }

    [Test]
    public async Task Choosing_the_display_name_needs_a_login() {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);

        var response = await client.PutAsJsonAsync("/api/users/settings/display-name", new DisplayNameUpdateRequest { Mode = DisplayNameMode.Username });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    /// <summary>The login method used to be guessed from the username being the e-mail, which no longer holds once usernames come from Keycloak.</summary>
    [Test]
    public async Task An_account_without_password_counts_as_sso() {
        var (ssoUser, ssoToken) = Builder.SeedAuthenticatedUser();
        Db.Users.Where(u => u.Id == ssoUser.Id).Set(u => u.Username, "erika.mustermann").Update();
        var (_, localToken) = Builder.SeedAuthenticatedUser(password: "lokal-geheim-123");

        await Assert.That((await SettingsOf(ssoToken)).IsSsoAccount).IsTrue();
        await Assert.That((await SettingsOf(localToken)).IsSsoAccount).IsFalse();
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
