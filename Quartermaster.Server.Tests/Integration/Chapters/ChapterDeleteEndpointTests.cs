using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Chapters;

public class ChapterDeleteEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_403_when_missing_permission() {
        var chapter = Builder.SeedChapter("X");
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.DeleteAsync($"/api/chapters/{chapter.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Soft_deletes_chapter_without_children() {
        var chapter = Builder.SeedChapter("Doomed");
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.DeleteChapter });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.DeleteAsync($"/api/chapters/{chapter.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        var stored = Db.Chapters.Single(c => c.Id == chapter.Id);
        await Assert.That(stored.DeletedAt).IsNotNull();
    }

    [Test]
    public async Task Returns_400_when_chapter_has_non_deleted_children() {
        var parent = Builder.SeedChapter("Parent");
        Builder.SeedChapter("Child", parentChapterId: parent.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.DeleteChapter });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.DeleteAsync($"/api/chapters/{parent.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        var stored = Db.Chapters.Single(c => c.Id == parent.Id);
        await Assert.That(stored.DeletedAt).IsNull();
    }

    [Test]
    public async Task Returns_404_when_chapter_does_not_exist() {
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.DeleteChapter });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.DeleteAsync($"/api/chapters/{Guid.NewGuid()}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Soft_deleted_child_does_not_block_parent_deletion() {
        var parent = Builder.SeedChapter("Parent");
        var child = Builder.SeedChapter("Child", parentChapterId: parent.Id);
        Db.Chapters.Where(c => c.Id == child.Id).Set(c => c.DeletedAt, DateTime.UtcNow).Update();

        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.DeleteChapter });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.DeleteAsync($"/api/chapters/{parent.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
    }
}
