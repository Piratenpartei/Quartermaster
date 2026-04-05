using System.Net;
using System.Threading.Tasks;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Security;

public class SecurityHeadersMiddlewareTests : IntegrationTestBase {
    [Test]
    public async Task X_Frame_Options_is_DENY() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/config/client");
        await Assert.That(response.Headers.GetValues("X-Frame-Options")).Contains("DENY");
    }

    [Test]
    public async Task X_Content_Type_Options_is_nosniff() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/config/client");
        await Assert.That(response.Headers.GetValues("X-Content-Type-Options")).Contains("nosniff");
    }

    [Test]
    public async Task Referrer_Policy_is_strict_origin_when_cross_origin() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/config/client");
        await Assert.That(response.Headers.GetValues("Referrer-Policy")).Contains("strict-origin-when-cross-origin");
    }

    [Test]
    public async Task CSP_header_is_set_with_wasm_unsafe_eval() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/config/client");
        var csp = string.Join(";", response.Headers.GetValues("Content-Security-Policy"));
        await Assert.That(csp).Contains("wasm-unsafe-eval");
        await Assert.That(csp).Contains("frame-ancestors 'none'");
        await Assert.That(csp).Contains("default-src 'self'");
    }

    [Test]
    public async Task HSTS_header_not_set_over_HTTP() {
        // Test client uses HTTP, so HSTS should not be set.
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/config/client");
        await Assert.That(response.Headers.Contains("Strict-Transport-Security")).IsFalse();
    }

    [Test]
    public async Task All_required_security_headers_present() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/config/client");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(response.Headers.Contains("X-Frame-Options")).IsTrue();
        await Assert.That(response.Headers.Contains("X-Content-Type-Options")).IsTrue();
        await Assert.That(response.Headers.Contains("Content-Security-Policy")).IsTrue();
        await Assert.That(response.Headers.Contains("Referrer-Policy")).IsTrue();
    }
}
