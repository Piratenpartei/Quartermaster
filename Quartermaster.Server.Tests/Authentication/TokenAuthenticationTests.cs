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

    [Test]
    public async Task Issued_token_has_expiry_populated_within_configured_lifetime() {
        var (user, _) = Builder.SeedAuthenticatedUser();
        var stored = Db.Tokens.Where(t => t.UserId == user.Id && t.Type == TokenType.Login).First();

        await Assert.That(stored.Expires).IsNotNull();
        await Assert.That(stored.Expires!.Value).IsGreaterThan(DateTime.UtcNow);
        // SeedAuthenticatedUser → TestDataBuilder.SeedLoginToken uses a 30-day far-future expiry
        // so test tokens never lapse mid-run. The exact value doesn't matter, only that one was set.
        await Assert.That(stored.Expires.Value).IsLessThan(DateTime.UtcNow.AddDays(31));
    }

    [Test]
    public async Task Successful_validation_slides_expiry_forward() {
        var (user, token) = Builder.SeedAuthenticatedUser();
        // Pull expiry close to now (but still valid) so the sliding-window extension is observable.
        var beforeExtension = DateTime.UtcNow.AddMinutes(5);
        Db.Tokens.Where(t => t.UserId == user.Id).Set(t => t.Expires, beforeExtension).Update();

        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/users/session");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var afterExtension = Db.Tokens.Where(t => t.UserId == user.Id).First().Expires;
        await Assert.That(afterExtension).IsNotNull();
        // Default lifetime is 7 days; after a successful validate the new expiry should be far past
        // the original 5-minute mark.
        await Assert.That(afterExtension!.Value).IsGreaterThan(beforeExtension.AddDays(1));
    }
}
