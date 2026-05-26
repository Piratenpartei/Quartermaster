using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api.Users;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Antiforgery;

/// <summary>
/// Direct tests for <c>AntiforgeryMiddleware</c>'s gate behavior — safe methods bypass,
/// non-API paths bypass, exempt paths bypass, missing/invalid CSRF token rejects.
/// </summary>
public class AntiforgeryMiddlewareTests : IntegrationTestBase {
    [Test]
    public async Task GET_request_bypasses_antiforgery() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/antiforgery/token");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task HEAD_request_bypasses_antiforgery() {
        using var client = AnonymousClient();
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/api/antiforgery/token"));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task POST_to_api_without_csrf_returns_403() {
        using var client = AnonymousClient();
        var response = await client.PostAsJsonAsync("/api/users/login",
            new LoginRequest { Username = "anyone", Password = "anything" });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body.Contains("Antiforgery")).IsTrue();
    }

    [Test]
    public async Task POST_to_api_with_valid_csrf_passes_through() {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync("/api/users/login",
            new LoginRequest { Username = "nobody-here", Password = "long-enough-password" });
        // CSRF passes (no 403 with text body), then the login fails on bad creds.
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task POST_to_saml_consume_is_exempt_from_csrf() {
        using var client = AnonymousClient();
        var response = await client.PostAsync("/api/users/SamlConsume",
            new FormUrlEncodedContent(new[] {
                new System.Collections.Generic.KeyValuePair<string, string>("SAMLResponse", "")
            }));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task POST_to_non_api_path_bypasses_csrf() {
        using var client = AnonymousClient();
        var response = await client.PostAsync("/random-non-api-path", new StringContent(""));
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Forbidden);
    }
}
