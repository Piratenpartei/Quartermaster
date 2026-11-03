using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Events;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Events;

/// <summary>Item ids are visible on public events; an officer's own event in the URL must not open another chapter's items.</summary>
public class ChecklistItemEventScopeTests : IntegrationTestBase {
    private Chapter _own = default!;
    private Event _ownEvent = default!;
    private Event _foreignEvent = default!;

    [Before(Test)]
    public void SeedEvents() {
        var root = Builder.SeedChapter("Bund");
        _own = Builder.SeedChapter("Ort", parentChapterId: root.Id);
        _ownEvent = Builder.SeedEvent(_own.Id);
        _foreignEvent = Builder.SeedEvent(root.Id);
    }

    private async Task<HttpClient> OwnOfficerAsync() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [_own.Id] = new[] { PermissionIdentifier.EditEvents } });
        return await AuthenticatedClientWithCsrfAsync(token);
    }

    private EventChecklistItem Reload(Guid itemId) {
        return Db.EventChecklistItems.Single(i => i.Id == itemId);
    }

    [Test]
    public async Task Check_of_another_events_item_gets_404() {
        var item = Builder.SeedChecklistItem(_foreignEvent.Id);
        using var client = await OwnOfficerAsync();

        var response = await client.PostAsJsonAsync($"/api/events/{_ownEvent.Id}/checklist/{item.Id}/check",
            new ChecklistItemCheckRequest { EventId = _ownEvent.Id, ItemId = item.Id, ExecuteAction = true });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(Reload(item.Id).IsCompleted).IsFalse();
    }

    [Test]
    public async Task Uncheck_of_another_events_item_gets_404() {
        var item = Builder.SeedChecklistItem(_foreignEvent.Id, isCompleted: true);
        using var client = await OwnOfficerAsync();

        var response = await client.PostAsJsonAsync($"/api/events/{_ownEvent.Id}/checklist/{item.Id}/uncheck", new { });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(Reload(item.Id).IsCompleted).IsTrue();
    }

    [Test]
    public async Task Update_of_another_events_item_gets_404() {
        var item = Builder.SeedChecklistItem(_foreignEvent.Id, label: "Original");
        using var client = await OwnOfficerAsync();

        var response = await client.PutAsJsonAsync($"/api/events/{_ownEvent.Id}/checklist/{item.Id}", new ChecklistItemUpdateRequest {
            EventId = _ownEvent.Id,
            ItemId = item.Id,
            Label = "Geändert",
            ItemType = ChecklistItemType.Text
        });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        var stored = Reload(item.Id);
        await Assert.That(stored.Label).IsEqualTo("Original");
        await Assert.That(stored.EventId).IsEqualTo(_foreignEvent.Id);
    }

    /// <summary>Delete stays idempotent: an item outside the event counts as already gone.</summary>
    [Test]
    public async Task Delete_of_another_events_item_deletes_nothing() {
        var item = Builder.SeedChecklistItem(_foreignEvent.Id);
        using var client = await OwnOfficerAsync();

        var response = await client.DeleteAsync($"/api/events/{_ownEvent.Id}/checklist/{item.Id}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.EventChecklistItems.Any(i => i.Id == item.Id)).IsTrue();
    }
}
