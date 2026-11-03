using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Data.Events;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Events;

public class EventSubscriptionEndpointTests : IntegrationTestBase {
    private static string Path(Guid eventId) => $"/api/events/{eventId}/subscription";

    private async Task<(Guid UserId, HttpClient Client)> UserClientAsync(Dictionary<Guid, string[]>? chapterPermissions = null) {
        var (user, token) = Builder.SeedAuthenticatedUser(chapterPermissions: chapterPermissions);
        var client = await AuthenticatedClientWithCsrfAsync(token);
        return (user.Id, client);
    }

    [Test]
    public async Task Anonymous_gets_401() {
        var ev = Builder.SeedEvent(Builder.SeedChapter().Id, visibility: EventVisibility.Public);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);

        await Assert.That((await client.PostAsJsonAsync(Path(ev.Id), new { })).StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That((await client.DeleteAsync(Path(ev.Id))).StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That((await client.GetAsync(Path(ev.Id))).StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That((await client.GetAsync("/api/users/event-subscriptions")).StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Missing_event_gets_404() {
        var (_, client) = await UserClientAsync();
        using (client) {
            await Assert.That((await client.PostAsJsonAsync(Path(Guid.NewGuid()), new { })).StatusCode).IsEqualTo(HttpStatusCode.NotFound);
            await Assert.That((await client.GetAsync(Path(Guid.NewGuid()))).StatusCode).IsEqualTo(HttpStatusCode.NotFound);
            await Assert.That((await client.DeleteAsync(Path(Guid.NewGuid()))).StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        }
    }

    [Test]
    public async Task Deleted_event_gets_404() {
        var ev = Builder.SeedEvent(Builder.SeedChapter().Id, visibility: EventVisibility.Public);
        Db.Events.Where(e => e.Id == ev.Id).Set(e => e.DeletedAt, DateTime.UtcNow).Update();
        var (_, client) = await UserClientAsync();
        using (client) {
            await Assert.That((await client.PostAsJsonAsync(Path(ev.Id), new { })).StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        }
    }

    [Test]
    public async Task Private_event_without_ViewEvents_gets_403() {
        var ev = Builder.SeedEvent(Builder.SeedChapter().Id, visibility: EventVisibility.Private);
        var (userId, client) = await UserClientAsync();
        using (client) {
            await Assert.That((await client.PostAsJsonAsync(Path(ev.Id), new { })).StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
            await Assert.That((await client.GetAsync(Path(ev.Id))).StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        }
        await Assert.That(Db.EventSubscriptions.Any(s => s.UserId == userId)).IsFalse();
    }

    [Test]
    public async Task Private_event_with_ViewEvents_can_be_subscribed() {
        var chapter = Builder.SeedChapter();
        var ev = Builder.SeedEvent(chapter.Id, visibility: EventVisibility.Private);
        var (userId, client) = await UserClientAsync(new() { [chapter.Id] = [PermissionIdentifier.ViewEvents] });
        using (client) {
            await Assert.That((await client.PostAsJsonAsync(Path(ev.Id), new { })).StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        }
        await Assert.That(Db.EventSubscriptions.Any(s => s.UserId == userId && s.EventId == ev.Id)).IsTrue();
    }

    [Test]
    [Arguments(EventStatus.Completed)]
    [Arguments(EventStatus.Archived)]
    public async Task Closed_event_subscribe_gets_409(EventStatus status) {
        var ev = Builder.SeedEvent(Builder.SeedChapter().Id, visibility: EventVisibility.Public, status: status);
        var (userId, client) = await UserClientAsync();
        using (client) {
            var response = await client.PostAsJsonAsync(Path(ev.Id), new { });
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
            await Assert.That(await response.Content.ReadAsStringAsync()).Contains("error.event.subscription_closed");
        }
        await Assert.That(Db.EventSubscriptions.Any(s => s.UserId == userId)).IsFalse();
    }

    [Test]
    public async Task Subscribe_twice_keeps_one_row() {
        var ev = Builder.SeedEvent(Builder.SeedChapter().Id, visibility: EventVisibility.MembersOnly);
        var (userId, client) = await UserClientAsync();
        using (client) {
            await Assert.That((await client.PostAsJsonAsync(Path(ev.Id), new { })).StatusCode).IsEqualTo(HttpStatusCode.NoContent);
            await Assert.That((await client.PostAsJsonAsync(Path(ev.Id), new { })).StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        }
        await Assert.That(Db.EventSubscriptions.Count(s => s.UserId == userId && s.EventId == ev.Id)).IsEqualTo(1);
    }

    [Test]
    public async Task State_reports_subscription_and_can_subscribe() {
        var ev = Builder.SeedEvent(Builder.SeedChapter().Id, visibility: EventVisibility.Public);
        var (_, client) = await UserClientAsync();
        using (client) {
            var before = await client.GetFromJsonAsync<EventSubscriptionStateDTO>(Path(ev.Id));
            await client.PostAsJsonAsync(Path(ev.Id), new { });
            var after = await client.GetFromJsonAsync<EventSubscriptionStateDTO>(Path(ev.Id));

            await Assert.That(before!.IsSubscribed).IsFalse();
            await Assert.That(before.CanSubscribe).IsTrue();
            await Assert.That(after!.IsSubscribed).IsTrue();
        }
    }

    [Test]
    public async Task State_of_completed_event_cannot_subscribe() {
        var ev = Builder.SeedEvent(Builder.SeedChapter().Id, visibility: EventVisibility.Public, status: EventStatus.Completed);
        var (_, client) = await UserClientAsync();
        using (client) {
            var state = await client.GetFromJsonAsync<EventSubscriptionStateDTO>(Path(ev.Id));
            await Assert.That(state!.CanSubscribe).IsFalse();
        }
    }

    [Test]
    public async Task Unsubscribe_works_for_completed_event() {
        var ev = Builder.SeedEvent(Builder.SeedChapter().Id, visibility: EventVisibility.Public);
        var (userId, client) = await UserClientAsync();
        using (client) {
            await client.PostAsJsonAsync(Path(ev.Id), new { });
            Db.Events.Where(e => e.Id == ev.Id).Set(e => e.Status, EventStatus.Completed).Update();

            await Assert.That((await client.DeleteAsync(Path(ev.Id))).StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        }
        await Assert.That(Db.EventSubscriptions.Any(s => s.UserId == userId)).IsFalse();
    }

    [Test]
    public async Task Unsubscribe_without_subscription_gets_204() {
        var ev = Builder.SeedEvent(Builder.SeedChapter().Id, visibility: EventVisibility.Public);
        var (_, client) = await UserClientAsync();
        using (client) {
            await Assert.That((await client.DeleteAsync(Path(ev.Id))).StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        }
    }

    [Test]
    public async Task List_returns_own_subscriptions_ordered_by_date() {
        var chapterId = Builder.SeedChapter().Id;
        var late = Builder.SeedEvent(chapterId, publicName: "Spaet", visibility: EventVisibility.Public, eventDate: new DateTime(2026, 12, 5));
        var early = Builder.SeedEvent(chapterId, publicName: "Frueh", visibility: EventVisibility.Public, eventDate: new DateTime(2026, 11, 28));
        var undated = Builder.SeedEvent(chapterId, publicName: "Offen", visibility: EventVisibility.Public);
        var foreign = Builder.SeedEvent(chapterId, publicName: "Fremd", visibility: EventVisibility.Public);
        var other = Builder.SeedUser();
        Db.Insert(new EventSubscription { EventId = foreign.Id, UserId = other.Id, CreatedAt = DateTime.UtcNow });
        var (_, client) = await UserClientAsync();
        using (client) {
            foreach (var ev in new[] { undated, late, early })
                await client.PostAsJsonAsync(Path(ev.Id), new { });

            var list = await client.GetFromJsonAsync<List<EventSubscriptionListItemDTO>>("/api/users/event-subscriptions");

            await Assert.That(string.Join(",", list!.Select(i => i.PublicName))).IsEqualTo("Frueh,Spaet,Offen");
            await Assert.That(list[0].EventId).IsEqualTo(early.Id);
            await Assert.That(list[2].EventDate).IsNull();
        }
    }

    [Test]
    public async Task List_hides_details_of_events_no_longer_visible() {
        var chapter = Builder.SeedChapter();
        var ev = Builder.SeedEvent(chapter.Id, publicName: "Geheim", visibility: EventVisibility.Private, eventDate: new DateTime(2026, 12, 5));
        var (userId, client) = await UserClientAsync(new() { [chapter.Id] = [PermissionIdentifier.ViewEvents] });
        using (client) {
            await client.PostAsJsonAsync(Path(ev.Id), new { });
            Db.UserChapterPermissions.Where(p => p.UserId == userId).Delete();

            var list = await client.GetFromJsonAsync<List<EventSubscriptionListItemDTO>>("/api/users/event-subscriptions");

            var item = list!.Single();
            await Assert.That(item.EventId).IsEqualTo(ev.Id);
            await Assert.That(item.IsVisible).IsFalse();
            await Assert.That(item.PublicName).IsEqualTo("");
            await Assert.That(item.EventDate).IsNull();
            await Assert.That((await client.DeleteAsync(Path(ev.Id))).StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        }
    }

    [Test]
    public async Task Unsubscribe_works_for_deleted_event() {
        var ev = Builder.SeedEvent(Builder.SeedChapter().Id, visibility: EventVisibility.Public);
        var (userId, client) = await UserClientAsync();
        using (client) {
            await client.PostAsJsonAsync(Path(ev.Id), new { });
            Db.Events.Where(e => e.Id == ev.Id).Set(e => e.DeletedAt, DateTime.UtcNow).Update();

            await Assert.That((await client.DeleteAsync(Path(ev.Id))).StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        }
        await Assert.That(Db.EventSubscriptions.Any(s => s.UserId == userId)).IsFalse();
    }
}
