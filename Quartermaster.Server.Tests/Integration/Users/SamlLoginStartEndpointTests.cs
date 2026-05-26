using System.Net;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Data.Options;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Users;

public class SamlLoginStartEndpointTests : IntegrationTestBase {
    private void SetSamlConfig(string? clientId, string? endpoint) {
        Db.SystemOptions.Where(o => o.Identifier == "auth.saml.client_id").Delete();
        Db.SystemOptions.Where(o => o.Identifier == "auth.saml.endpoint").Delete();
        if (clientId != null) {
            Db.Insert(new SystemOption { Identifier = "auth.saml.client_id", Value = clientId });
        }
        if (endpoint != null) {
            Db.Insert(new SystemOption { Identifier = "auth.saml.endpoint", Value = endpoint });
        }
    }

    [Test]
    public async Task Returns_503_when_client_id_unset() {
        SetSamlConfig(clientId: null, endpoint: "https://idp.example.com/saml");
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/users/SamlLoginStart");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.ServiceUnavailable);
    }

    [Test]
    public async Task Returns_503_when_endpoint_unset() {
        SetSamlConfig(clientId: "quartermaster", endpoint: null);
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/users/SamlLoginStart");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.ServiceUnavailable);
    }

    [Test]
    public async Task Redirects_to_idp_endpoint_when_configured() {
        SetSamlConfig(clientId: "quartermaster", endpoint: "https://idp.example.com/saml");
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/users/SamlLoginStart");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
        await Assert.That(response.Headers.Location!.ToString().StartsWith("https://idp.example.com/saml")).IsTrue();
    }
}
