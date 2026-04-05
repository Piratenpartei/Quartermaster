using System;
using System.Net;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Data.Tokens;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Authentication;

public class TokenAuthenticationTests : IntegrationTestBase {
    [Test]
    public async Task Missing_authorization_header_returns_401() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/users/session");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Malformed_authorization_header_returns_401() {
        using var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", "notatoken");
        var response = await client.GetAsync("/api/users/session");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Invalid_bearer_token_returns_401() {
        using var client = AuthenticatedClient("not-a-valid-token-content");
        var response = await client.GetAsync("/api/users/session");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Empty_bearer_token_returns_401() {
        using var client = AuthenticatedClient("");
        var response = await client.GetAsync("/api/users/session");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Expired_token_returns_401() {
        var (user, token) = Builder.SeedAuthenticatedUser();
        // Set expiry in the past
        Db.Tokens.Where(t => t.UserId == user.Id).Set(t => t.Expires, DateTime.UtcNow.AddDays(-1)).Update();

        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/users/session");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Token_for_deleted_user_returns_401() {
        var (user, token) = Builder.SeedAuthenticatedUser();
        // Hard-delete the user
        Db.Users.Where(u => u.Id == user.Id).Delete();

        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/users/session");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Valid_bearer_token_authenticates_successfully() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/users/session");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Token_with_only_login_type_authenticates_not_donation_type() {
        var (user, _) = Builder.SeedAuthenticatedUser();
        // Manually insert a DonationMarker-type token
        var marker = Guid.NewGuid().ToString("N");
        Db.Insert(new Token {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Content = marker,
            Type = TokenType.DonationMarker
        });
        using var client = AuthenticatedClient(marker);
        var response = await client.GetAsync("/api/users/session");
        // DonationMarker type should not authenticate login requests
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Whitespace_bearer_token_returns_401() {
        using var client = AuthenticatedClient("   ");
        var response = await client.GetAsync("/api/users/session");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }
}
