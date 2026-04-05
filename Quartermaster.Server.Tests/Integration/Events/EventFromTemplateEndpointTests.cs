using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Events;

public class EventFromTemplateEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        var chapter = Builder.SeedChapter("C");
        var template = Builder.SeedEventTemplate(chapterId: chapter.Id);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync("/api/events/from-template", new EventFromTemplateRequest {
            TemplateId = template.Id,
            ChapterId = chapter.Id,
            VariableValues = new Dictionary<string, string>()
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_CreateEvents() {
        var chapter = Builder.SeedChapter("C");
        var template = Builder.SeedEventTemplate(chapterId: chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/events/from-template", new EventFromTemplateRequest {
            TemplateId = template.Id,
            ChapterId = chapter.Id,
            VariableValues = new Dictionary<string, string>()
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_404_when_template_does_not_exist() {
        var chapter = Builder.SeedChapter("C");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.CreateEvents } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/events/from-template", new EventFromTemplateRequest {
            TemplateId = Guid.NewGuid(),
            ChapterId = chapter.Id,
            VariableValues = new Dictionary<string, string>()
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Returns_400_when_template_id_empty() {
        var chapter = Builder.SeedChapter("C");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.CreateEvents } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/events/from-template", new EventFromTemplateRequest {
            TemplateId = Guid.Empty,
            ChapterId = chapter.Id,
            VariableValues = new Dictionary<string, string>()
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Creates_event_from_template_with_variable_substitution() {
        var chapter = Builder.SeedChapter("C");
        var template = Builder.SeedEventTemplate(chapterId: chapter.Id, name: "Tpl");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.CreateEvents } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/events/from-template", new EventFromTemplateRequest {
            TemplateId = template.Id,
            ChapterId = chapter.Id,
            VariableValues = new Dictionary<string, string> { ["name"] = "Hello" }
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<EventDetailDTO>();
        await Assert.That(dto!.PublicName).IsEqualTo("Hello");
        await Assert.That(dto.EventTemplateId).IsEqualTo(template.Id);
        await Assert.That(Db.Events.Any(e => e.Id == dto.Id)).IsTrue();
    }
}
