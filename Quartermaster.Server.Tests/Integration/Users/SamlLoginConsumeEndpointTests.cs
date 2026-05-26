using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Data.Options;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Users;

/// <summary>
/// Covers the error paths of the SAML consume endpoint. The happy path requires a
/// signed SAML XML response against a real X.509 cert — fixture-prep beyond the
/// scope of integration tests here. Replay protection is exercised at the
/// repository level by <c>UsedSamlAssertionRepositoryTests</c>.
/// </summary>
public class SamlLoginConsumeEndpointTests : IntegrationTestBase {
    private void SetCert(string? certBase64) {
        Db.SystemOptions.Where(o => o.Identifier == "auth.saml.certificate").Delete();
        if (certBase64 != null) {
            Db.Insert(new SystemOption { Identifier = "auth.saml.certificate", Value = certBase64 });
        }
    }

    private static FormUrlEncodedContent Form(string samlData) =>
        new(new[] { new KeyValuePair<string, string>("SAMLResponse", samlData) });

    [Test]
    public async Task Returns_400_when_saml_data_missing() {
        SetCert("dGVzdA==");
        using var client = AnonymousClient();
        var response = await client.PostAsync("/api/users/SamlConsume", Form(""));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Returns_503_when_certificate_unset() {
        SetCert(null);
        using var client = AnonymousClient();
        var response = await client.PostAsync("/api/users/SamlConsume", Form("<not-real-but-non-empty/>"));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.ServiceUnavailable);
    }

    [Test]
    public async Task Redirects_with_saml_invalid_when_response_unparseable() {
        SetCert("dGVzdA==");
        using var client = AnonymousClient();
        var response = await client.PostAsync("/api/users/SamlConsume", Form("garbage"));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
        await Assert.That(response.Headers.Location!.ToString()).IsEqualTo("/Login?error=saml_invalid");
    }
}
