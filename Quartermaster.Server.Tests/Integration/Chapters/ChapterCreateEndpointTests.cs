using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.Chapters;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Chapters;

public class ChapterCreateEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync("/api/chapters", new ChapterCreateRequest { Name = "X" });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_missing_permission() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/chapters", new ChapterCreateRequest { Name = "X" });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Creates_top_level_chapter() {
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.CreateChapter });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/chapters", new ChapterCreateRequest {
            Name = "Bundesverband",
            ShortCode = "BV",
            ExternalCode = "DE-001"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var dto = await response.Content.ReadFromJsonAsync<ChapterDTO>();
        await Assert.That(dto!.Name).IsEqualTo("Bundesverband");
        await Assert.That(dto.ShortCode).IsEqualTo("BV");
        await Assert.That(dto.ExternalCode).IsEqualTo("DE-001");
        await Assert.That(dto.ParentChapterId).IsNull();

        var stored = Db.Chapters.Single(c => c.Id == dto.Id);
        await Assert.That(stored.Name).IsEqualTo("Bundesverband");
    }

    [Test]
    public async Task Creates_sub_chapter_under_parent() {
        var parent = Builder.SeedChapter("Bundesverband");
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.CreateChapter });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/chapters", new ChapterCreateRequest {
            Name = "Landesverband Berlin",
            ParentChapterId = parent.Id
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var dto = await response.Content.ReadFromJsonAsync<ChapterDTO>();
        await Assert.That(dto!.ParentChapterId).IsEqualTo(parent.Id);
    }

    [Test]
    public async Task Returns_400_when_name_missing() {
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.CreateChapter });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/chapters", new ChapterCreateRequest { Name = "   " });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Returns_400_when_parent_does_not_exist() {
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.CreateChapter });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/chapters", new ChapterCreateRequest {
            Name = "Orphan",
            ParentChapterId = System.Guid.NewGuid()
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Returns_400_when_external_code_collides_under_same_parent() {
        var parent = Builder.SeedChapter("Bundesverband");
        Builder.SeedChapter("First", parentChapterId: parent.Id, externalCode: "DUP");
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.CreateChapter });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/chapters", new ChapterCreateRequest {
            Name = "Second",
            ParentChapterId = parent.Id,
            ExternalCode = "DUP"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Same_external_code_under_different_parents_is_allowed() {
        var parentA = Builder.SeedChapter("A");
        var parentB = Builder.SeedChapter("B");
        Builder.SeedChapter("Under A", parentChapterId: parentA.Id, externalCode: "X-1");

        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.CreateChapter });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PostAsJsonAsync("/api/chapters", new ChapterCreateRequest {
            Name = "Under B",
            ParentChapterId = parentB.Id,
            ExternalCode = "X-1"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
