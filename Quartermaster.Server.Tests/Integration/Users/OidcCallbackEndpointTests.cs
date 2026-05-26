using System.Net;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Data.Options;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Users;

public class OidcCallbackEndpointTests : IntegrationTestBase {
    private void SetOidcConfig(string? authority = "https://idp.example.com", string? clientId = "my-client") {
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
    public async Task Redirects_with_idp_error_when_error_parameter_present() {
        SetOidcConfig();
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/users/OidcCallback?error=access_denied&error_description=user_cancelled");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
        await Assert.That(response.Headers.Location!.ToString()).IsEqualTo("/Login?error=oidc_idp_error");
    }

    [Test]
    public async Task Redirects_with_no_code_when_code_parameter_missing() {
        SetOidcConfig();
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/users/OidcCallback?state=abc");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
        await Assert.That(response.Headers.Location!.ToString()).IsEqualTo("/Login?error=oidc_no_code");
    }

    [Test]
    public async Task Redirects_with_not_configured_when_authority_unset() {
        SetOidcConfig(authority: null);
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/users/OidcCallback?code=abc&state=xyz");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
        await Assert.That(response.Headers.Location!.ToString()).IsEqualTo("/Login?error=oidc_not_configured");
    }

    [Test]
    public async Task Redirects_with_expired_when_cookies_missing() {
        SetOidcConfig();
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/users/OidcCallback?code=abc&state=xyz");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
        await Assert.That(response.Headers.Location!.ToString()).IsEqualTo("/Login?error=oidc_expired");
    }

    [Test]
    public async Task Redirects_with_state_mismatch_when_state_does_not_match_cookie() {
        SetOidcConfig();
        using var client = AnonymousClient();
        // Inject all three required cookies manually
        client.DefaultRequestHeaders.Add("Cookie", "oidc_cv=verifier; oidc_state=expected; oidc_nonce=nonce");
        var response = await client.GetAsync("/api/users/OidcCallback?code=abc&state=wrong");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
        await Assert.That(response.Headers.Location!.ToString()).IsEqualTo("/Login?error=oidc_state");
    }
}
