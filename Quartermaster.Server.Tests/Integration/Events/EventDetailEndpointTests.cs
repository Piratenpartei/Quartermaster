using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Events;

public class EventDetailEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_public_event_to_anonymous() {
        var chapter = Builder.SeedChapter("C");
        var ev = Builder.SeedEvent(chapter.Id, visibility: EventVisibility.Public);
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/events/{ev.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<EventDetailDTO>();
        await Assert.That(dto!.Id).IsEqualTo(ev.Id);
    }

    [Test]
    public async Task Returns_401_for_anonymous_on_membersonly_event() {
        var chapter = Builder.SeedChapter("C");
        var ev = Builder.SeedEvent(chapter.Id, visibility: EventVisibility.MembersOnly);
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/events/{ev.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_401_for_anonymous_on_private_event() {
        var chapter = Builder.SeedChapter("C");
        var ev = Builder.SeedEvent(chapter.Id, visibility: EventVisibility.Private);
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/events/{ev.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_on_private_event_when_user_lacks_ViewEvents() {
        var chapter = Builder.SeedChapter("C");
        var ev = Builder.SeedEvent(chapter.Id, visibility: EventVisibility.Private);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/events/{ev.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_membersonly_event_to_authenticated() {
        var chapter = Builder.SeedChapter("C");
        var ev = Builder.SeedEvent(chapter.Id, visibility: EventVisibility.MembersOnly);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/events/{ev.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Returns_private_event_when_user_has_ViewEvents() {
        var chapter = Builder.SeedChapter("C");
        var ev = Builder.SeedEvent(chapter.Id, visibility: EventVisibility.Private);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.ViewEvents } });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/events/{ev.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Returns_404_for_nonexistent_id() {
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/events/{Guid.NewGuid()}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
