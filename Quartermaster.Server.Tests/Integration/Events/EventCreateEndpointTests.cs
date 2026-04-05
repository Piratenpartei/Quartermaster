using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Events;

public class EventCreateEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        var chapter = Builder.SeedChapter("C");
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync("/api/events", new EventCreateRequest {
            ChapterId = chapter.Id,
            InternalName = "X",
            PublicName = "X"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_CreateEvents() {
        var chapter = Builder.SeedChapter("C");
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/events", new EventCreateRequest {
            ChapterId = chapter.Id,
            InternalName = "X",
            PublicName = "X"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Creates_event_with_defaults_Draft_Private() {
        var chapter = Builder.SeedChapter("C");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.CreateEvents } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/events", new EventCreateRequest {
            ChapterId = chapter.Id,
            InternalName = "Internal",
            PublicName = "Public"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<EventDetailDTO>();
        await Assert.That(dto!.Status).IsEqualTo(EventStatus.Draft);
        await Assert.That(dto.Visibility).IsEqualTo(EventVisibility.Private);
        await Assert.That(dto.InternalName).IsEqualTo("Internal");
        await Assert.That(dto.PublicName).IsEqualTo("Public");
    }

    [Test]
    public async Task Returns_400_when_internal_name_empty() {
        var chapter = Builder.SeedChapter("C");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.CreateEvents } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/events", new EventCreateRequest {
            ChapterId = chapter.Id,
            InternalName = "",
            PublicName = "Public"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Returns_400_when_chapter_id_empty() {
        var chapter = Builder.SeedChapter("C");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.CreateEvents } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/events", new EventCreateRequest {
            ChapterId = Guid.Empty,
            InternalName = "X",
            PublicName = "X"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Persists_event_in_database() {
        var chapter = Builder.SeedChapter("C");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.CreateEvents } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/events", new EventCreateRequest {
            ChapterId = chapter.Id,
            InternalName = "Saved",
            PublicName = "Public Saved",
            Visibility = EventVisibility.MembersOnly
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var persisted = Db.Events.Where(e => e.InternalName == "Saved").ToList();
        await Assert.That(persisted.Count).IsEqualTo(1);
        await Assert.That(persisted[0].Visibility).IsEqualTo(EventVisibility.MembersOnly);
    }
}
