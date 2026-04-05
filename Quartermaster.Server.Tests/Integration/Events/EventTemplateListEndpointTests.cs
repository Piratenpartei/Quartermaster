using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Events;

public class EventTemplateListEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/eventtemplates");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_ViewTemplates() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/eventtemplates");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_templates_for_user_with_chapter_ViewTemplates() {
        var chapter = Builder.SeedChapter("C");
        Builder.SeedEventTemplate(chapterId: chapter.Id, name: "Tpl1");
        Builder.SeedEventTemplate(chapterId: chapter.Id, name: "Tpl2");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.ViewTemplates } });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/eventtemplates");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<List<EventTemplateDTO>>();
        await Assert.That(list!.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Returns_empty_list_when_no_templates_in_scope() {
        var chapter = Builder.SeedChapter("C");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.ViewTemplates } });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/eventtemplates");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<List<EventTemplateDTO>>();
        await Assert.That(list!.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Filters_templates_by_allowed_chapter() {
        var chapterA = Builder.SeedChapter("A");
        var chapterB = Builder.SeedChapter("B");
        Builder.SeedEventTemplate(chapterId: chapterA.Id, name: "TplA");
        Builder.SeedEventTemplate(chapterId: chapterB.Id, name: "TplB");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterA.Id] = new[] { PermissionIdentifier.ViewTemplates } });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/eventtemplates");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<List<EventTemplateDTO>>();
        await Assert.That(list!.Count).IsEqualTo(1);
        await Assert.That(list[0].Name).IsEqualTo("TplA");
    }
}
