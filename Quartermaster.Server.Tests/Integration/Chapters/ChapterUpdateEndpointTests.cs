using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.Chapters;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Chapters;

public class ChapterUpdateEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_403_when_missing_permission() {
        var chapter = Builder.SeedChapter("X");
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PutAsJsonAsync($"/api/chapters/{chapter.Id}", new ChapterUpdateRequest { Name = "Y" });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Updates_all_editable_fields() {
        var newParent = Builder.SeedChapter("New Parent");
        var chapter = Builder.SeedChapter("Old Name", shortCode: "OLD", externalCode: "EXT-OLD");
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.EditChapter });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PutAsJsonAsync($"/api/chapters/{chapter.Id}", new ChapterUpdateRequest {
            Name = "New Name",
            ShortCode = "NEW",
            ExternalCode = "EXT-NEW",
            ParentChapterId = newParent.Id
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var stored = Db.Chapters.Single(c => c.Id == chapter.Id);
        await Assert.That(stored.Name).IsEqualTo("New Name");
        await Assert.That(stored.ShortCode).IsEqualTo("NEW");
        await Assert.That(stored.ExternalCode).IsEqualTo("EXT-NEW");
        await Assert.That(stored.ParentChapterId).IsEqualTo(newParent.Id);
    }

    [Test]
    public async Task Updates_administrative_division_link() {
        var div = Builder.SeedAdminDivision("Niedersachsen");
        var chapter = Builder.SeedChapter("LV");
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.EditChapter });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PutAsJsonAsync($"/api/chapters/{chapter.Id}", new ChapterUpdateRequest {
            Name = "LV",
            AdministrativeDivisionId = div.Id
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var stored = Db.Chapters.Single(c => c.Id == chapter.Id);
        await Assert.That(stored.AdministrativeDivisionId).IsEqualTo(div.Id);
    }

    [Test]
    public async Task Clearing_administrative_division_unsets_it() {
        var div = Builder.SeedAdminDivision("Niedersachsen");
        var chapter = Builder.SeedChapter("LV", adminDivisionId: div.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.EditChapter });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PutAsJsonAsync($"/api/chapters/{chapter.Id}", new ChapterUpdateRequest {
            Name = "LV",
            AdministrativeDivisionId = null
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var stored = Db.Chapters.Single(c => c.Id == chapter.Id);
        await Assert.That(stored.AdministrativeDivisionId).IsNull();
    }

    [Test]
    public async Task Clearing_parent_makes_it_top_level() {
        var parent = Builder.SeedChapter("Parent");
        var child = Builder.SeedChapter("Child", parentChapterId: parent.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.EditChapter });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PutAsJsonAsync($"/api/chapters/{child.Id}", new ChapterUpdateRequest {
            Name = child.Name,
            ParentChapterId = null
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var stored = Db.Chapters.Single(c => c.Id == child.Id);
        await Assert.That(stored.ParentChapterId).IsNull();
    }

    [Test]
    public async Task Returns_400_when_setting_self_as_parent() {
        var chapter = Builder.SeedChapter("X");
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.EditChapter });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PutAsJsonAsync($"/api/chapters/{chapter.Id}", new ChapterUpdateRequest {
            Name = "X",
            ParentChapterId = chapter.Id
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Returns_404_when_chapter_does_not_exist() {
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.EditChapter });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PutAsJsonAsync($"/api/chapters/{Guid.NewGuid()}", new ChapterUpdateRequest { Name = "X" });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Same_external_code_kept_on_self_is_allowed() {
        var chapter = Builder.SeedChapter("X", externalCode: "EXT-1");
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.EditChapter });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PutAsJsonAsync($"/api/chapters/{chapter.Id}", new ChapterUpdateRequest {
            Name = "X renamed",
            ExternalCode = "EXT-1"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task External_code_collision_with_sibling_rejected() {
        var parent = Builder.SeedChapter("P");
        Builder.SeedChapter("A", parentChapterId: parent.Id, externalCode: "DUP");
        var b = Builder.SeedChapter("B", parentChapterId: parent.Id, externalCode: "OTHER");

        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.EditChapter });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PutAsJsonAsync($"/api/chapters/{b.Id}", new ChapterUpdateRequest {
            Name = "B",
            ParentChapterId = parent.Id,
            ExternalCode = "DUP"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}
