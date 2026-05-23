using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Options;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Options;

public class OptionListEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/options");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_view_options() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/options");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_seeded_option_definitions() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewOptions });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/options");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<List<OptionDefinitionDTO>>();
        await Assert.That(list).IsNotNull();
        // Option definitions are seeded by DbContext.SupplementDefaults at startup.
        await Assert.That(list!.Count > 0).IsTrue();
    }

    [Test]
    public async Task Includes_global_value_when_set() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewOptions });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/options");
        var list = await response.Content.ReadFromJsonAsync<List<OptionDefinitionDTO>>();
        // Default-seeded option "auth.lockout.max_attempts" has default value "5"
        var attempt = list!.FirstOrDefault(d => d.Identifier == "auth.lockout.max_attempts");
        await Assert.That(attempt).IsNotNull();
        await Assert.That(attempt!.GlobalValue).IsEqualTo("5");
    }

    [Test]
    public async Task Secret_option_value_is_masked_without_ViewOptionSecrets_permission() {
        var editor = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewOptions, PermissionIdentifier.EditOptions });
        using var writeClient = await AuthenticatedClientWithCsrfAsync(editor.Token);
        var setResp = await writeClient.PostAsJsonAsync("/api/options", new OptionUpdateRequest {
            Identifier = "email.smtp.password",
            ChapterId = null,
            Value = "hunter2-supersecret"
        });
        await Assert.That(setResp.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var (_, viewerToken) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewOptions });
        using var viewClient = AuthenticatedClient(viewerToken);
        var response = await viewClient.GetAsync("/api/options");
        var list = await response.Content.ReadFromJsonAsync<List<OptionDefinitionDTO>>();
        var pw = list!.First(d => d.Identifier == "email.smtp.password");
        await Assert.That(pw.IsSecret).IsTrue();
        await Assert.That(pw.ValueMasked).IsTrue();
        await Assert.That(pw.GlobalValue).IsNotEqualTo("hunter2-supersecret");
        await Assert.That(pw.GlobalValue.Contains("hunter2")).IsFalse();
    }

    [Test]
    public async Task Secret_option_value_is_plaintext_with_ViewOptionSecrets_permission() {
        var editor = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewOptions, PermissionIdentifier.EditOptions });
        using var writeClient = await AuthenticatedClientWithCsrfAsync(editor.Token);
        await writeClient.PostAsJsonAsync("/api/options", new OptionUpdateRequest {
            Identifier = "email.smtp.password",
            ChapterId = null,
            Value = "hunter2-supersecret"
        });

        var (_, adminToken) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewOptions, PermissionIdentifier.ViewOptionSecrets });
        using var client = AuthenticatedClient(adminToken);
        var response = await client.GetAsync("/api/options");
        var list = await response.Content.ReadFromJsonAsync<List<OptionDefinitionDTO>>();
        var pw = list!.First(d => d.Identifier == "email.smtp.password");
        await Assert.That(pw.IsSecret).IsTrue();
        await Assert.That(pw.ValueMasked).IsFalse();
        await Assert.That(pw.GlobalValue).IsEqualTo("hunter2-supersecret");
    }

    [Test]
    public async Task Non_secret_options_are_never_masked() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewOptions });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/options");
        var list = await response.Content.ReadFromJsonAsync<List<OptionDefinitionDTO>>();
        var nonSecret = list!.First(d => d.Identifier == "auth.lockout.max_attempts");
        await Assert.That(nonSecret.IsSecret).IsFalse();
        await Assert.That(nonSecret.ValueMasked).IsFalse();
        await Assert.That(nonSecret.GlobalValue).IsEqualTo("5");
    }

    [Test]
    public async Task Includes_chapter_override_in_response() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewOptions, PermissionIdentifier.EditOptions });
        var chapter = Builder.SeedChapter("Test Ch");
        // Set an override via POST so the endpoint stores it
        using var csrfClient = await AuthenticatedClientWithCsrfAsync(token);
        var setResp = await csrfClient.PostAsJsonAsync("/api/options", new OptionUpdateRequest {
            Identifier = "general.contact.email",
            ChapterId = chapter.Id,
            Value = "override@test.local"
        });
        await Assert.That(setResp.StatusCode).IsEqualTo(HttpStatusCode.OK);

        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/options");
        var list = await response.Content.ReadFromJsonAsync<List<OptionDefinitionDTO>>();
        var contact = list!.First(d => d.Identifier == "general.contact.email");
        await Assert.That(contact.Overrides.Count).IsEqualTo(1);
        await Assert.That(contact.Overrides[0].Value).IsEqualTo("override@test.local");
    }
}
