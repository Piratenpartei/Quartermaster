using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Events;

public class EventStatusUpdateEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        var chapter = Builder.SeedChapter("C");
        var ev = Builder.SeedEvent(chapter.Id);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PutAsJsonAsync($"/api/events/{ev.Id}/status",
            new EventStatusUpdateRequestBody { Status = EventStatus.Active });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_EditEvents() {
        var chapter = Builder.SeedChapter("C");
        var ev = Builder.SeedEvent(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/events/{ev.Id}/status",
            new EventStatusUpdateRequestBody { Status = EventStatus.Active });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_404_when_event_does_not_exist() {
        var chapter = Builder.SeedChapter("C");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditEvents } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/events/{Guid.NewGuid()}/status",
            new EventStatusUpdateRequestBody { Status = EventStatus.Active });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Allows_Draft_to_Active_transition() {
        var chapter = Builder.SeedChapter("C");
        var ev = Builder.SeedEvent(chapter.Id, status: EventStatus.Draft);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditEvents } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/events/{ev.Id}/status",
            new EventStatusUpdateRequestBody { Status = EventStatus.Active });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updated = Db.Events.Where(e => e.Id == ev.Id).First();
        await Assert.That(updated.Status).IsEqualTo(EventStatus.Active);
    }

    [Test]
    public async Task Rejects_invalid_Draft_to_Completed_transition() {
        var chapter = Builder.SeedChapter("C");
        var ev = Builder.SeedEvent(chapter.Id, status: EventStatus.Draft);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditEvents } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/events/{ev.Id}/status",
            new EventStatusUpdateRequestBody { Status = EventStatus.Completed });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Rejects_same_status_transition() {
        var chapter = Builder.SeedChapter("C");
        var ev = Builder.SeedEvent(chapter.Id, status: EventStatus.Draft);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditEvents } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/events/{ev.Id}/status",
            new EventStatusUpdateRequestBody { Status = EventStatus.Draft });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Allows_Completed_to_Archived_with_DeleteEvents() {
        var chapter = Builder.SeedChapter("C");
        var ev = Builder.SeedEvent(chapter.Id, status: EventStatus.Completed);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.DeleteEvents } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/events/{ev.Id}/status",
            new EventStatusUpdateRequestBody { Status = EventStatus.Archived });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    private class EventStatusUpdateRequestBody {
        public EventStatus Status { get; set; }
    }
}
