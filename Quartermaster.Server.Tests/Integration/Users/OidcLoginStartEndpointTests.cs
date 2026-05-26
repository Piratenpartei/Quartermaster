using System;
using System.Net;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Data.Options;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Users;

public class OidcLoginStartEndpointTests : IntegrationTestBase {
    private void SetOidcConfig(string? authority, string? clientId) {
        Db.SystemOptions.Where(o => o.Identifier == "auth.oidc.authority").Delete();
        Db.SystemOptions.Where(o => o.Identifier == "auth.oidc.client_id").Delete();
        if (authority != null) {
            Db.Insert(new SystemOption { Identifier = "auth.oidc.authority", Value = authority });
        }
        if (clientId != null) {
            Db.Insert(new SystemOption { Identifier = "auth.oidc.client_id", Value = clientId });
        }
    }

    [Test]
    public async Task Returns_503_when_authority_unset() {
        SetOidcConfig(authority: null, clientId: "client");
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/users/OidcLoginStart");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.ServiceUnavailable);
    }

    [Test]
    public async Task Returns_503_when_client_id_unset() {
        SetOidcConfig(authority: "https://idp.example.com", clientId: null);
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/users/OidcLoginStart");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.ServiceUnavailable);
    }

    [Test]
    public async Task Redirects_to_authority_with_pkce_and_state_when_configured() {
        SetOidcConfig(authority: "https://idp.example.com", clientId: "my-client");
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/users/OidcLoginStart");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);

        var location = response.Headers.Location!.ToString();
        await Assert.That(location.StartsWith("https://idp.example.com/protocol/openid-connect/auth")).IsTrue();
        await Assert.That(location.Contains("client_id=my-client")).IsTrue();
        await Assert.That(location.Contains("code_challenge_method=S256")).IsTrue();
        await Assert.That(location.Contains("code_challenge=")).IsTrue();
        await Assert.That(location.Contains("state=")).IsTrue();
        await Assert.That(location.Contains("nonce=")).IsTrue();
    }

    [Test]
    public async Task Sets_PKCE_state_and_nonce_cookies() {
        SetOidcConfig(authority: "https://idp.example.com", clientId: "my-client");
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/users/OidcLoginStart");
        var setCookies = response.Headers.GetValues("Set-Cookie");
        await Assert.That(setCookies.Any(c => c.StartsWith("oidc_cv="))).IsTrue();
        await Assert.That(setCookies.Any(c => c.StartsWith("oidc_state="))).IsTrue();
        await Assert.That(setCookies.Any(c => c.StartsWith("oidc_nonce="))).IsTrue();
    }
}
