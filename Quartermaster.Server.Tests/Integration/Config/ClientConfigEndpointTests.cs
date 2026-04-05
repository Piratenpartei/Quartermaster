using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api.Config;
using Quartermaster.Data.Options;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Config;

public class ClientConfigEndpointTests : IntegrationTestBase {
    private void UpsertOption(string identifier, string value) {
        var existing = Db.SystemOptions.Where(o => o.Identifier == identifier && o.ChapterId == null).FirstOrDefault();
        if (existing != null) {
            Db.SystemOptions.Where(o => o.Id == existing.Id).Set(o => o.Value, value).Update();
        } else {
            Db.Insert(new SystemOption {
                Id = Guid.NewGuid(),
                Identifier = identifier,
                Value = value,
                ChapterId = null
            });
        }
    }

    /// <summary>
    /// Forces the test host to boot (which runs <c>SupplementDefaults</c> and seeds default
    /// SystemOptions). Call this before <see cref="UpsertOption"/> so our custom values survive.
    /// </summary>
    private static async Task WarmUpAsync(HttpClient client) {
        var resp = await client.GetAsync("/api/config/client");
        resp.EnsureSuccessStatusCode();
    }

    [Test]
    public async Task Returns_200_to_anonymous_caller() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/config/client");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Returns_defaults_when_no_options_set() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/config/client");
        var dto = await response.Content.ReadFromJsonAsync<ClientConfigDTO>();
        await Assert.That(dto).IsNotNull();
        await Assert.That(dto!.SamlEnabled).IsFalse();
        await Assert.That(dto.OidcEnabled).IsFalse();
        await Assert.That(dto.ShowDetailedErrors).IsFalse();
    }

    [Test]
    public async Task SamlEnabled_true_when_endpoint_configured() {
        using var client = AnonymousClient();
        await WarmUpAsync(client);
        UpsertOption("auth.saml.endpoint", "https://idp.example.com/saml");
        var response = await client.GetAsync("/api/config/client");
        var dto = await response.Content.ReadFromJsonAsync<ClientConfigDTO>();
        await Assert.That(dto!.SamlEnabled).IsTrue();
    }

    [Test]
    public async Task OidcEnabled_true_when_authority_configured() {
        using var client = AnonymousClient();
        await WarmUpAsync(client);
        UpsertOption("auth.oidc.authority", "https://idp.example.com");
        var response = await client.GetAsync("/api/config/client");
        var dto = await response.Content.ReadFromJsonAsync<ClientConfigDTO>();
        await Assert.That(dto!.OidcEnabled).IsTrue();
    }

    [Test]
    public async Task Returns_configured_error_contact_and_button_texts() {
        using var client = AnonymousClient();
        await WarmUpAsync(client);
        UpsertOption("general.error.contact", "admin@example.com");
        UpsertOption("auth.saml.button_text", "Sign in with IdP");
        var response = await client.GetAsync("/api/config/client");
        var dto = await response.Content.ReadFromJsonAsync<ClientConfigDTO>();
        await Assert.That(dto!.ErrorContact).IsEqualTo("admin@example.com");
        await Assert.That(dto.SamlButtonText).IsEqualTo("Sign in with IdP");
    }

    [Test]
    public async Task ShowDetailedErrors_true_when_option_set_to_true() {
        using var client = AnonymousClient();
        await WarmUpAsync(client);
        UpsertOption("general.error.show_details", "true");
        var response = await client.GetAsync("/api/config/client");
        var dto = await response.Content.ReadFromJsonAsync<ClientConfigDTO>();
        await Assert.That(dto!.ShowDetailedErrors).IsTrue();
    }
}
