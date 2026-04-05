using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Events;

public class ChecklistItemReorderEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        var chapter = Builder.SeedChapter("C");
        var ev = Builder.SeedEvent(chapter.Id);
        var item = Builder.SeedChecklistItem(ev.Id, sortOrder: 0);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync(
            $"/api/events/{ev.Id}/checklist/{item.Id}/reorder", new { Direction = 1 });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_EditEvents() {
        var chapter = Builder.SeedChapter("C");
        var ev = Builder.SeedEvent(chapter.Id);
        var item = Builder.SeedChecklistItem(ev.Id, sortOrder: 0);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync(
            $"/api/events/{ev.Id}/checklist/{item.Id}/reorder", new { Direction = 1 });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_404_when_event_does_not_exist() {
        var chapter = Builder.SeedChapter("C");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditEvents } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync(
            $"/api/events/{Guid.NewGuid()}/checklist/{Guid.NewGuid()}/reorder", new { Direction = 1 });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Returns_400_for_invalid_direction() {
        var chapter = Builder.SeedChapter("C");
        var ev = Builder.SeedEvent(chapter.Id);
        var item = Builder.SeedChecklistItem(ev.Id, sortOrder: 0);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditEvents } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync(
            $"/api/events/{ev.Id}/checklist/{item.Id}/reorder", new { Direction = 5 });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Swaps_sort_order_when_moving_down() {
        var chapter = Builder.SeedChapter("C");
        var ev = Builder.SeedEvent(chapter.Id);
        var itemA = Builder.SeedChecklistItem(ev.Id, sortOrder: 0, label: "A");
        var itemB = Builder.SeedChecklistItem(ev.Id, sortOrder: 1, label: "B");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditEvents } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync(
            $"/api/events/{ev.Id}/checklist/{itemA.Id}/reorder", new { Direction = 1 });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updatedA = Db.EventChecklistItems.Where(i => i.Id == itemA.Id).First();
        var updatedB = Db.EventChecklistItems.Where(i => i.Id == itemB.Id).First();
        await Assert.That(updatedA.SortOrder).IsEqualTo(1);
        await Assert.That(updatedB.SortOrder).IsEqualTo(0);
    }
}
