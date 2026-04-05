using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Events;

public class ChecklistItemCheckEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        var chapter = Builder.SeedChapter("C");
        var ev = Builder.SeedEvent(chapter.Id);
        var item = Builder.SeedChecklistItem(ev.Id);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync($"/api/events/{ev.Id}/checklist/{item.Id}/check", new { });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_EditEvents() {
        var chapter = Builder.SeedChapter("C");
        var ev = Builder.SeedEvent(chapter.Id);
        var item = Builder.SeedChecklistItem(ev.Id);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync($"/api/events/{ev.Id}/checklist/{item.Id}/check", new { });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_404_when_event_does_not_exist() {
        var chapter = Builder.SeedChapter("C");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditEvents } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync(
            $"/api/events/{Guid.NewGuid()}/checklist/{Guid.NewGuid()}/check", new { });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Returns_404_when_item_does_not_exist() {
        var chapter = Builder.SeedChapter("C");
        var ev = Builder.SeedEvent(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditEvents } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync(
            $"/api/events/{ev.Id}/checklist/{Guid.NewGuid()}/check", new { });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Rejects_already_completed_item() {
        var chapter = Builder.SeedChapter("C");
        var ev = Builder.SeedEvent(chapter.Id);
        var item = Builder.SeedChecklistItem(ev.Id, isCompleted: true);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditEvents } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync($"/api/events/{ev.Id}/checklist/{item.Id}/check", new { });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Checks_item_and_transitions_Draft_to_Active() {
        var chapter = Builder.SeedChapter("C");
        var ev = Builder.SeedEvent(chapter.Id, status: EventStatus.Draft);
        var item = Builder.SeedChecklistItem(ev.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditEvents } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync($"/api/events/{ev.Id}/checklist/{item.Id}/check", new { });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updatedItem = Db.EventChecklistItems.Where(i => i.Id == item.Id).First();
        await Assert.That(updatedItem.IsCompleted).IsTrue();
        var updatedEvent = Db.Events.Where(e => e.Id == ev.Id).First();
        await Assert.That(updatedEvent.Status).IsEqualTo(EventStatus.Active);
    }
}
