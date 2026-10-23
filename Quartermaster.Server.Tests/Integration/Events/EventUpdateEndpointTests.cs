using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Events;

public class EventUpdateEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        var chapter = Builder.SeedChapter("C");
        var ev = Builder.SeedEvent(chapter.Id);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PutAsJsonAsync($"/api/events/{ev.Id}", new EventUpdateRequest {
            Id = ev.Id,
            InternalName = "X",
            PublicName = "X"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_EditEvents() {
        var chapter = Builder.SeedChapter("C");
        var ev = Builder.SeedEvent(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/events/{ev.Id}", new EventUpdateRequest {
            Id = ev.Id,
            InternalName = "X",
            PublicName = "X"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_404_when_event_does_not_exist() {
        var chapter = Builder.SeedChapter("C");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditEvents } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var missing = Guid.NewGuid();
        var response = await client.PutAsJsonAsync($"/api/events/{missing}", new EventUpdateRequest {
            Id = missing,
            InternalName = "X",
            PublicName = "X"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Returns_400_when_public_name_empty() {
        var chapter = Builder.SeedChapter("C");
        var ev = Builder.SeedEvent(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditEvents } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/events/{ev.Id}", new EventUpdateRequest {
            Id = ev.Id,
            InternalName = "Internal",
            PublicName = ""
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Updates_event_fields() {
        var chapter = Builder.SeedChapter("C");
        var ev = Builder.SeedEvent(chapter.Id, internalName: "Before", publicName: "BeforePublic");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditEvents } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/events/{ev.Id}", new EventUpdateRequest {
            Id = ev.Id,
            InternalName = "After",
            PublicName = "AfterPublic",
            Visibility = EventVisibility.Public
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updated = Db.Events.Where(e => e.Id == ev.Id).First();
        await Assert.That(updated.InternalName).IsEqualTo("After");
        await Assert.That(updated.PublicName).IsEqualTo("AfterPublic");
        await Assert.That(updated.Visibility).IsEqualTo(EventVisibility.Public);
    }

    [Test]
    public async Task Saves_start_time_and_meeting_link() {
        var chapter = Builder.SeedChapter("C");
        var ev = Builder.SeedEvent(chapter.Id, visibility: EventVisibility.Public);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditEvents } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PutAsJsonAsync($"/api/events/{ev.Id}", new EventUpdateRequest {
            Id = ev.Id,
            InternalName = "KPT",
            PublicName = "Kreisparteitag",
            EventDate = new DateOnly(2026, 11, 28),
            StartTime = new TimeOnly(10, 30),
            MeetingUrl = " https://meet.jit.si/kpt-celle ",
            Visibility = EventVisibility.Public
        });
        using var anonymous = AnonymousClient();
        var detail = await anonymous.GetFromJsonAsync<EventDetailDTO>($"/api/events/{ev.Id}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(detail!.StartTime).IsEqualTo(new TimeOnly(10, 30));
        await Assert.That(detail.MeetingUrl).IsEqualTo("https://meet.jit.si/kpt-celle");
    }

    [Test]
    public async Task Returns_400_for_a_plain_http_meeting_link() {
        var chapter = Builder.SeedChapter("C");
        var ev = Builder.SeedEvent(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditEvents } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PutAsJsonAsync($"/api/events/{ev.Id}", new EventUpdateRequest {
            Id = ev.Id,
            InternalName = "KPT",
            PublicName = "Kreisparteitag",
            MeetingUrl = "http://meet.jit.si/kpt-celle"
        });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}
