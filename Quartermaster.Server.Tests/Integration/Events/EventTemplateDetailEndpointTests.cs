using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Events;

public class EventTemplateDetailEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        var chapter = Builder.SeedChapter("C");
        var template = Builder.SeedEventTemplate(chapterId: chapter.Id);
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/eventtemplates/{template.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_ViewTemplates() {
        var chapter = Builder.SeedChapter("C");
        var template = Builder.SeedEventTemplate(chapterId: chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/eventtemplates/{template.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_404_when_template_does_not_exist() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewTemplates });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/eventtemplates/{Guid.NewGuid()}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Returns_template_detail_when_authorized() {
        var chapter = Builder.SeedChapter("C");
        var template = Builder.SeedEventTemplate(chapterId: chapter.Id, name: "MyTemplate");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.ViewTemplates } });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/eventtemplates/{template.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<EventTemplateDetailDTO>();
        await Assert.That(dto!.Id).IsEqualTo(template.Id);
        await Assert.That(dto.Name).IsEqualTo("MyTemplate");
    }

    [Test]
    public async Task Global_template_requires_global_ViewTemplates() {
        var template = Builder.SeedEventTemplate(chapterId: null, name: "Global");
        var chapter = Builder.SeedChapter("C");
        var (_, tokenChapter) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.ViewTemplates } });
        using var chapterClient = AuthenticatedClient(tokenChapter);
        var chapterResp = await chapterClient.GetAsync($"/api/eventtemplates/{template.Id}");
        await Assert.That(chapterResp.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);

        var (_, tokenGlobal) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewTemplates });
        using var globalClient = AuthenticatedClient(tokenGlobal);
        var globalResp = await globalClient.GetAsync($"/api/eventtemplates/{template.Id}");
        await Assert.That(globalResp.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
