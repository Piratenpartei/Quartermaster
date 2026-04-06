using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Meetings;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Meetings;

public class MeetingStatusUpdateEndpointTests : IntegrationTestBase {
    private class StatusBody {
        public MeetingStatus Status { get; set; }
    }

    [Test]
    public async Task Returns_401_when_anonymous() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PutAsJsonAsync($"/api/meetings/{meeting.Id}/status",
            new StatusBody { Status = MeetingStatus.Scheduled });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_EditMeetings() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/meetings/{meeting.Id}/status",
            new StatusBody { Status = MeetingStatus.Scheduled });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Rejects_Draft_to_Completed_transition() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: MeetingStatus.Draft);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/meetings/{meeting.Id}/status",
            new StatusBody { Status = MeetingStatus.Completed });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Rejects_InProgress_to_Scheduled_transition() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: MeetingStatus.InProgress, meetingDate: DateTime.UtcNow);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/meetings/{meeting.Id}/status",
            new StatusBody { Status = MeetingStatus.Scheduled });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Accepts_Draft_to_Scheduled_when_date_set() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: MeetingStatus.Draft, meetingDate: DateTime.UtcNow.AddDays(7));
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/meetings/{meeting.Id}/status",
            new StatusBody { Status = MeetingStatus.Scheduled });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updated = Db.Meetings.First(m => m.Id == meeting.Id);
        await Assert.That(updated.Status).IsEqualTo(MeetingStatus.Scheduled);
    }

    [Test]
    public async Task Rejects_transition_to_Scheduled_without_date() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: MeetingStatus.Draft, meetingDate: null);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/meetings/{meeting.Id}/status",
            new StatusBody { Status = MeetingStatus.Scheduled });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Archive_requires_DeleteMeetings() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: MeetingStatus.Completed);
        // User has EditMeetings but not DeleteMeetings
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/meetings/{meeting.Id}/status",
            new StatusBody { Status = MeetingStatus.Archived });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Archive_succeeds_with_DeleteMeetings() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: MeetingStatus.Completed);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.DeleteMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/meetings/{meeting.Id}/status",
            new StatusBody { Status = MeetingStatus.Archived });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
