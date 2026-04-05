using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Options;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Options;

public class OptionUpdateEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync("/api/options", new OptionUpdateRequest {
            Identifier = "general.contact.email",
            Value = "x"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_edit_options() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewOptions });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/options", new OptionUpdateRequest {
            Identifier = "general.contact.email",
            Value = "x@y"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_404_for_unknown_option() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.EditOptions });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/options", new OptionUpdateRequest {
            Identifier = "nonexistent.option.xyz",
            Value = "abc"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Returns_400_when_identifier_empty() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.EditOptions });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/options", new OptionUpdateRequest {
            Identifier = "",
            Value = "abc"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Updates_global_value() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.EditOptions });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/options", new OptionUpdateRequest {
            Identifier = "general.contact.email",
            ChapterId = null,
            Value = "new@test.local"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var stored = Db.SystemOptions.First(o =>
            o.Identifier == "general.contact.email" && o.ChapterId == null);
        await Assert.That(stored.Value).IsEqualTo("new@test.local");
    }

    [Test]
    public async Task Rejects_chapter_override_on_non_overridable_option() {
        var chapter = Builder.SeedChapter("C");
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.EditOptions });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        // "member_import.file_path" is seeded with IsOverridable=false
        var response = await client.PostAsJsonAsync("/api/options", new OptionUpdateRequest {
            Identifier = "member_import.file_path",
            ChapterId = chapter.Id,
            Value = "/some/path"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}
