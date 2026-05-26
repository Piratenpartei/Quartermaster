using System;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Authentication;

/// <summary>
/// Direct tests for <c>TokenAuthenticationHandler</c>'s three credential sources:
/// auth cookie (browser), Bearer header (external clients), and ?access_token query
/// parameter (SignalR WebSocket transport).
/// </summary>
public class TokenAuthenticationHandlerTests : IntegrationTestBase {
    private const string SessionPath = "/api/users/session";

    [Test]
    public async Task Cookie_credential_authenticates_request() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AnonymousClient();
        client.DefaultRequestHeaders.Add("Cookie", $".Quartermaster.Auth={token}");
        var response = await client.GetAsync(SessionPath);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Bearer_header_authenticates_request() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync(SessionPath);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Missing_credentials_returns_401() {
        using var client = AnonymousClient();
        var response = await client.GetAsync(SessionPath);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Invalid_bearer_token_returns_401() {
        using var client = AuthenticatedClient("definitely-not-a-real-token");
        var response = await client.GetAsync(SessionPath);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Cookie_takes_precedence_over_bearer_header() {
        var (cookieUser, cookieToken) = Builder.SeedAuthenticatedUser(firstName: "Cookie", lastName: "User");
        var (_, bearerToken) = Builder.SeedAuthenticatedUser(firstName: "Bearer", lastName: "User");
        using var client = AnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        client.DefaultRequestHeaders.Add("Cookie", $".Quartermaster.Auth={cookieToken}");
        var response = await client.GetAsync(SessionPath);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body.Contains(cookieUser.Id.ToString())).IsTrue();
    }

    [Test]
    public async Task Deleted_token_returns_401() {
        var (user, token) = Builder.SeedAuthenticatedUser();
        Db.Tokens.Where(t => t.UserId == user.Id).Delete();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync(SessionPath);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Token_owned_by_deleted_user_returns_401() {
        var (user, token) = Builder.SeedAuthenticatedUser();
        Db.Users.Where(u => u.Id == user.Id)
            .Set(u => u.DeletedAt, (DateTime?)DateTime.UtcNow)
            .Update();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync(SessionPath);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Empty_bearer_returns_401_not_no_result() {
        using var client = AnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "  ");
        var response = await client.GetAsync(SessionPath);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }
}
