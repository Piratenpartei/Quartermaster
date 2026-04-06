using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.Meetings;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Meetings;

public class AgendaItemStartEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: MeetingStatus.InProgress);
        var item = Builder.SeedAgendaItem(meeting.Id);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/{item.Id}/start", new { });
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
            $"/api/meetings/{meeting.Id}/agenda/{item.Id}/start", new { });
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
            $"/api/meetings/{meeting.Id}/agenda/{item.Id}/start", new { });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Sets_started_at_timestamp() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: MeetingStatus.InProgress);
        var item = Builder.SeedAgendaItem(meeting.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/{item.Id}/start", new { });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updated = Db.AgendaItems.First(a => a.Id == item.Id);
        await Assert.That(updated.StartedAt).IsNotNull();
    }

    [Test]
    public async Task Auto_completes_previous_in_progress_item() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: MeetingStatus.InProgress);
        var previous = Builder.SeedAgendaItem(meeting.Id, title: "Prev");
        var next = Builder.SeedAgendaItem(meeting.Id, title: "Next");
        // Simulate previous item being in progress.
        Db.AgendaItems.Where(a => a.Id == previous.Id)
            .Set(a => a.StartedAt, DateTime.UtcNow.AddMinutes(-5))
            .Update();
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/{next.Id}/start", new { });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var prevUpdated = Db.AgendaItems.First(a => a.Id == previous.Id);
        await Assert.That(prevUpdated.CompletedAt).IsNotNull();
        var nextUpdated = Db.AgendaItems.First(a => a.Id == next.Id);
        await Assert.That(nextUpdated.StartedAt).IsNotNull();
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
            $"/api/meetings/{meeting.Id}/agenda/{fake}/start", new { });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
