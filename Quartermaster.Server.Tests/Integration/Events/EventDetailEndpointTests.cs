using System;
using System.Collections.Generic;
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

    private async Task<EventDetailDTO> DetailFor(Guid eventId, Guid chapterId, params string[] permissions) {
        var (_, token) = Builder.SeedAuthenticatedUser(chapterPermissions: new Dictionary<Guid, string[]> {
            [chapterId] = permissions
        });
        using var client = AuthenticatedClient(token);
        var dto = await client.GetFromJsonAsync<EventDetailDTO>($"/api/events/{eventId}");
        return dto!;
    }

    [Test]
    public async Task View_only_user_gets_no_edit_flags() {
        var chapter = Builder.SeedChapter("C");
        var ev = Builder.SeedEvent(chapter.Id);

        var dto = await DetailFor(ev.Id, chapter.Id, PermissionIdentifier.ViewEvents);

        await Assert.That(dto.CanEdit).IsFalse();
        await Assert.That(dto.CanArchive).IsFalse();
        await Assert.That(dto.CanSaveAsTemplate).IsFalse();
    }

    [Test]
    public async Task Edit_flags_follow_the_permissions() {
        var chapter = Builder.SeedChapter("C");
        var ev = Builder.SeedEvent(chapter.Id);

        var edit = await DetailFor(ev.Id, chapter.Id, PermissionIdentifier.ViewEvents, PermissionIdentifier.EditEvents);
        var archive = await DetailFor(ev.Id, chapter.Id, PermissionIdentifier.ViewEvents, PermissionIdentifier.DeleteEvents);
        var template = await DetailFor(ev.Id, chapter.Id, PermissionIdentifier.ViewEvents, PermissionIdentifier.EditTemplates);

        await Assert.That(edit.CanEdit).IsTrue();
        await Assert.That(edit.CanArchive).IsFalse();
        await Assert.That(archive.CanArchive).IsTrue();
        await Assert.That(archive.CanEdit).IsFalse();
        await Assert.That(template.CanSaveAsTemplate).IsTrue();
        await Assert.That(template.CanEdit).IsFalse();
    }

    [Test]
    public async Task Anonymous_gets_no_edit_flags() {
        var chapter = Builder.SeedChapter("C");
        var ev = Builder.SeedEvent(chapter.Id, visibility: EventVisibility.Public);
        using var client = AnonymousClient();

        var dto = await client.GetFromJsonAsync<EventDetailDTO>($"/api/events/{ev.Id}");

        await Assert.That(dto!.CanEdit).IsFalse();
        await Assert.That(dto.CanArchive).IsFalse();
        await Assert.That(dto.CanSaveAsTemplate).IsFalse();
    }
}
