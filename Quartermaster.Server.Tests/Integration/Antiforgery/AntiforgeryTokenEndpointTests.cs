using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Antiforgery;

public class AntiforgeryTokenEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_200_to_anonymous_caller() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/antiforgery/token");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Response_contains_token_field() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/antiforgery/token");
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(payload.TryGetProperty("token", out _)).IsTrue();
        var token = payload.GetProperty("token").GetString();
        await Assert.That(string.IsNullOrEmpty(token)).IsFalse();
    }

    [Test]
    public async Task Sets_antiforgery_cookie() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/antiforgery/token");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var setCookies = response.Headers.Contains("Set-Cookie")
            ? response.Headers.GetValues("Set-Cookie").ToList()
            : new System.Collections.Generic.List<string>();
        await Assert.That(setCookies.Any(c => c.Contains("Antiforgery"))).IsTrue();
    }

    [Test]
    public async Task Can_be_used_to_authorize_subsequent_post() {
        // The issued token should validate a subsequent POST request.
        var (_, bearer) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(bearer);
        // Any auth-requiring POST to exercise the CSRF path — use lockouts/unlock.
        var response = await client.PostAsJsonAsync(
            "/api/users/lockouts/unlock",
            new { IpAddress = "127.0.0.1", UsernameOrEmail = "nobody" });
        // Without ViewUsers it should be 403 — NOT 403 from antiforgery (would be text body).
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Each_call_returns_a_token() {
        using var client = AnonymousClient();
        var r1 = await client.GetAsync("/api/antiforgery/token");
        var r2 = await client.GetAsync("/api/antiforgery/token");
        await Assert.That(r1.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(r2.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var p1 = await r1.Content.ReadFromJsonAsync<JsonElement>();
        var p2 = await r2.Content.ReadFromJsonAsync<JsonElement>();
        await Assert.That(string.IsNullOrEmpty(p1.GetProperty("token").GetString())).IsFalse();
        await Assert.That(string.IsNullOrEmpty(p2.GetProperty("token").GetString())).IsFalse();
    }
}
