using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Meetings;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Meetings;

public class AgendaItemCompleteEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: MeetingStatus.InProgress);
        var item = Builder.SeedAgendaItem(meeting.Id);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/{item.Id}/complete", new { });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_EditMeetings() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: MeetingStatus.InProgress);
        var item = Builder.SeedAgendaItem(meeting.Id);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/{item.Id}/complete", new { });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_400_when_meeting_not_in_progress() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: MeetingStatus.Scheduled);
        var item = Builder.SeedAgendaItem(meeting.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/{item.Id}/complete", new { });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Sets_completed_at_timestamp() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: MeetingStatus.InProgress);
        var item = Builder.SeedAgendaItem(meeting.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/{item.Id}/complete", new { });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updated = Db.AgendaItems.First(a => a.Id == item.Id);
        await Assert.That(updated.CompletedAt).IsNotNull();
    }

    [Test]
    public async Task Returns_404_when_item_not_in_meeting() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: MeetingStatus.InProgress);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var fake = Guid.NewGuid();
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/{fake}/complete", new { });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
