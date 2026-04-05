using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Events;

public class EventTemplateDeleteEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        var chapter = Builder.SeedChapter("C");
        var template = Builder.SeedEventTemplate(chapterId: chapter.Id);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.DeleteAsync($"/api/eventtemplates/{template.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_EditTemplates() {
        var chapter = Builder.SeedChapter("C");
        var template = Builder.SeedEventTemplate(chapterId: chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.DeleteAsync($"/api/eventtemplates/{template.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_404_when_template_does_not_exist() {
        var chapter = Builder.SeedChapter("C");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditTemplates } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.DeleteAsync($"/api/eventtemplates/{Guid.NewGuid()}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Deletes_chapter_template_when_authorized() {
        var chapter = Builder.SeedChapter("C");
        var template = Builder.SeedEventTemplate(chapterId: chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditTemplates } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.DeleteAsync($"/api/eventtemplates/{template.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var remaining = Db.EventTemplates.Where(t => t.Id == template.Id).Count();
        await Assert.That(remaining).IsEqualTo(0);
    }

    [Test]
    public async Task Global_template_deletion_requires_global_EditTemplates() {
        var template = Builder.SeedEventTemplate(chapterId: null, name: "Global");
        var chapter = Builder.SeedChapter("C");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditTemplates } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.DeleteAsync($"/api/eventtemplates/{template.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }
}
