using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Events;

public class EventListEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Anonymous_sees_only_public_events() {
        var chapter = Builder.SeedChapter("Chapter");
        Builder.SeedEvent(chapter.Id, visibility: EventVisibility.Public, internalName: "Pub");
        Builder.SeedEvent(chapter.Id, visibility: EventVisibility.MembersOnly, internalName: "Members");
        Builder.SeedEvent(chapter.Id, visibility: EventVisibility.Private, internalName: "Priv");
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/events");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<EventSearchResponse>();
        await Assert.That(result!.Items.Count).IsEqualTo(1);
        await Assert.That(result.Items[0].Visibility).IsEqualTo(EventVisibility.Public);
    }

    [Test]
    public async Task Authenticated_without_ViewEvents_sees_public_and_membersonly() {
        var chapter = Builder.SeedChapter("Chapter");
        Builder.SeedEvent(chapter.Id, visibility: EventVisibility.Public);
        Builder.SeedEvent(chapter.Id, visibility: EventVisibility.MembersOnly);
        Builder.SeedEvent(chapter.Id, visibility: EventVisibility.Private);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/events");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<EventSearchResponse>();
        await Assert.That(result!.Items.Count).IsEqualTo(2);
        await Assert.That(result.Items.Any(e => e.Visibility == EventVisibility.Private)).IsFalse();
    }

    [Test]
    public async Task Authenticated_with_ViewEvents_sees_all_visibilities() {
        var chapter = Builder.SeedChapter("Chapter");
        Builder.SeedEvent(chapter.Id, visibility: EventVisibility.Public);
        Builder.SeedEvent(chapter.Id, visibility: EventVisibility.MembersOnly);
        Builder.SeedEvent(chapter.Id, visibility: EventVisibility.Private);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.ViewEvents } });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/events");
        var result = await response.Content.ReadFromJsonAsync<EventSearchResponse>();
        await Assert.That(result!.Items.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Excludes_archived_by_default() {
        var chapter = Builder.SeedChapter("Chapter");
        Builder.SeedEvent(chapter.Id, visibility: EventVisibility.Public, status: EventStatus.Active);
        Builder.SeedEvent(chapter.Id, visibility: EventVisibility.Public, status: EventStatus.Archived);
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/events");
        var result = await response.Content.ReadFromJsonAsync<EventSearchResponse>();
        await Assert.That(result!.Items.Count).IsEqualTo(1);
        await Assert.That(result.Items[0].Status).IsEqualTo(EventStatus.Active);
    }

    [Test]
    public async Task Includes_archived_when_flag_set() {
        var chapter = Builder.SeedChapter("Chapter");
        Builder.SeedEvent(chapter.Id, visibility: EventVisibility.Public, status: EventStatus.Active);
        Builder.SeedEvent(chapter.Id, visibility: EventVisibility.Public, status: EventStatus.Archived);
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/events?IncludeArchived=true");
        var result = await response.Content.ReadFromJsonAsync<EventSearchResponse>();
        await Assert.That(result!.Items.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Filters_by_chapter_id() {
        var chapterA = Builder.SeedChapter("A");
        var chapterB = Builder.SeedChapter("B");
        Builder.SeedEvent(chapterA.Id, visibility: EventVisibility.Public, internalName: "EvA");
        Builder.SeedEvent(chapterB.Id, visibility: EventVisibility.Public, internalName: "EvB");
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/events?ChapterId={chapterA.Id}");
        var result = await response.Content.ReadFromJsonAsync<EventSearchResponse>();
        await Assert.That(result!.Items.Count).IsEqualTo(1);
        await Assert.That(result.Items[0].ChapterId).IsEqualTo(chapterA.Id);
    }

    [Test]
    public async Task Rejects_page_size_over_100() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/events?Page=1&PageSize=500");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}
