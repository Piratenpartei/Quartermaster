using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Meetings;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Meetings;

public class AgendaItemDeleteEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        var item = Builder.SeedAgendaItem(meeting.Id);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.DeleteAsync($"/api/meetings/{meeting.Id}/agenda/{item.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_EditMeetings() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        var item = Builder.SeedAgendaItem(meeting.Id);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.DeleteAsync($"/api/meetings/{meeting.Id}/agenda/{item.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Deletes_item_when_meeting_is_Draft() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: MeetingStatus.Draft);
        var item = Builder.SeedAgendaItem(meeting.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.DeleteAsync($"/api/meetings/{meeting.Id}/agenda/{item.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var gone = Db.AgendaItems.Any(a => a.Id == item.Id);
        await Assert.That(gone).IsFalse();
    }

    [Test]
    public async Task Blocks_deletion_when_meeting_is_Completed() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: MeetingStatus.Completed);
        var item = Builder.SeedAgendaItem(meeting.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.DeleteAsync($"/api/meetings/{meeting.Id}/agenda/{item.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Blocks_deletion_when_meeting_is_Archived() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: MeetingStatus.Archived);
        var item = Builder.SeedAgendaItem(meeting.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.DeleteAsync($"/api/meetings/{meeting.Id}/agenda/{item.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Returns_404_when_item_not_in_meeting() {
        var chapter = Builder.SeedChapter("C");
        var meetingA = Builder.SeedMeeting(chapter.Id, title: "A");
        var meetingB = Builder.SeedMeeting(chapter.Id, title: "B");
        var itemInB = Builder.SeedAgendaItem(meetingB.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.DeleteAsync($"/api/meetings/{meetingA.Id}/agenda/{itemInB.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
