using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Meetings;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Meetings;

public class AgendaItemNotesEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        var item = Builder.SeedAgendaItem(meeting.Id);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PutAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/{item.Id}/notes",
            new AgendaItemNotesRequest { MeetingId = meeting.Id, ItemId = item.Id, Notes = "Note" });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_EditMeetings() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        var item = Builder.SeedAgendaItem(meeting.Id);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/{item.Id}/notes",
            new AgendaItemNotesRequest { MeetingId = meeting.Id, ItemId = item.Id, Notes = "Note" });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Updates_notes_field() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        var item = Builder.SeedAgendaItem(meeting.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/{item.Id}/notes",
            new AgendaItemNotesRequest { MeetingId = meeting.Id, ItemId = item.Id, Notes = "Live notes" });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updated = Db.AgendaItems.First(a => a.Id == item.Id);
        await Assert.That(updated.Notes).IsEqualTo("Live notes");
    }

    [Test]
    public async Task Returns_404_when_item_not_in_meeting() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var fake = Guid.NewGuid();
        var response = await client.PutAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/{fake}/notes",
            new AgendaItemNotesRequest { MeetingId = meeting.Id, ItemId = fake, Notes = "X" });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Accepts_null_notes() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        var item = Builder.SeedAgendaItem(meeting.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/{item.Id}/notes",
            new AgendaItemNotesRequest { MeetingId = meeting.Id, ItemId = item.Id, Notes = null });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
