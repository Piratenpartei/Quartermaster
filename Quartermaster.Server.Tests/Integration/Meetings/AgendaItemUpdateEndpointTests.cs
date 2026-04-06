using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Meetings;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Meetings;

public class AgendaItemUpdateEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        var item = Builder.SeedAgendaItem(meeting.Id);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PutAsJsonAsync($"/api/meetings/{meeting.Id}/agenda/{item.Id}", new AgendaItemUpdateRequest {
            MeetingId = meeting.Id,
            ItemId = item.Id,
            Title = "X",
            ItemType = AgendaItemType.Discussion
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_EditMeetings() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        var item = Builder.SeedAgendaItem(meeting.Id);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/meetings/{meeting.Id}/agenda/{item.Id}", new AgendaItemUpdateRequest {
            MeetingId = meeting.Id,
            ItemId = item.Id,
            Title = "X",
            ItemType = AgendaItemType.Discussion
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_404_when_item_not_found() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var fakeId = Guid.NewGuid();
        var response = await client.PutAsJsonAsync($"/api/meetings/{meeting.Id}/agenda/{fakeId}", new AgendaItemUpdateRequest {
            MeetingId = meeting.Id,
            ItemId = fakeId,
            Title = "X",
            ItemType = AgendaItemType.Discussion
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Updates_title_and_notes() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        var item = Builder.SeedAgendaItem(meeting.Id, title: "Old");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/meetings/{meeting.Id}/agenda/{item.Id}", new AgendaItemUpdateRequest {
            MeetingId = meeting.Id,
            ItemId = item.Id,
            Title = "New",
            ItemType = AgendaItemType.Discussion,
            Notes = "Some notes"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updated = Db.AgendaItems.First(a => a.Id == item.Id);
        await Assert.That(updated.Title).IsEqualTo("New");
        await Assert.That(updated.Notes).IsEqualTo("Some notes");
    }

    [Test]
    public async Task Returns_400_when_title_empty() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        var item = Builder.SeedAgendaItem(meeting.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/meetings/{meeting.Id}/agenda/{item.Id}", new AgendaItemUpdateRequest {
            MeetingId = meeting.Id,
            ItemId = item.Id,
            Title = "",
            ItemType = AgendaItemType.Discussion
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Returns_400_when_motion_type_without_motion_id() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        var item = Builder.SeedAgendaItem(meeting.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/meetings/{meeting.Id}/agenda/{item.Id}", new AgendaItemUpdateRequest {
            MeetingId = meeting.Id,
            ItemId = item.Id,
            Title = "Motion",
            ItemType = AgendaItemType.Motion,
            MotionId = null
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}
