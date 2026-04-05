using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Events;

public class ChecklistItemUpdateEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        var chapter = Builder.SeedChapter("C");
        var ev = Builder.SeedEvent(chapter.Id);
        var item = Builder.SeedChecklistItem(ev.Id);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PutAsJsonAsync($"/api/events/{ev.Id}/checklist/{item.Id}",
            new ChecklistItemUpdateRequest {
                EventId = ev.Id,
                ItemId = item.Id,
                Label = "Updated",
                ItemType = 0
            });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_EditEvents() {
        var chapter = Builder.SeedChapter("C");
        var ev = Builder.SeedEvent(chapter.Id);
        var item = Builder.SeedChecklistItem(ev.Id);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/events/{ev.Id}/checklist/{item.Id}",
            new ChecklistItemUpdateRequest {
                EventId = ev.Id,
                ItemId = item.Id,
                Label = "Updated",
                ItemType = 0
            });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_404_when_event_does_not_exist() {
        var chapter = Builder.SeedChapter("C");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditEvents } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var missingEvent = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var response = await client.PutAsJsonAsync($"/api/events/{missingEvent}/checklist/{itemId}",
            new ChecklistItemUpdateRequest {
                EventId = missingEvent,
                ItemId = itemId,
                Label = "X",
                ItemType = 0
            });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Returns_400_when_label_empty() {
        var chapter = Builder.SeedChapter("C");
        var ev = Builder.SeedEvent(chapter.Id);
        var item = Builder.SeedChecklistItem(ev.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditEvents } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/events/{ev.Id}/checklist/{item.Id}",
            new ChecklistItemUpdateRequest {
                EventId = ev.Id,
                ItemId = item.Id,
                Label = "",
                ItemType = 0
            });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Updates_item_fields() {
        var chapter = Builder.SeedChapter("C");
        var ev = Builder.SeedEvent(chapter.Id);
        var item = Builder.SeedChecklistItem(ev.Id, label: "Old", sortOrder: 1);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditEvents } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/events/{ev.Id}/checklist/{item.Id}",
            new ChecklistItemUpdateRequest {
                EventId = ev.Id,
                ItemId = item.Id,
                Label = "New",
                SortOrder = 7,
                ItemType = 0
            });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updated = Db.EventChecklistItems.Where(i => i.Id == item.Id).First();
        await Assert.That(updated.Label).IsEqualTo("New");
        await Assert.That(updated.SortOrder).IsEqualTo(7);
    }
}
