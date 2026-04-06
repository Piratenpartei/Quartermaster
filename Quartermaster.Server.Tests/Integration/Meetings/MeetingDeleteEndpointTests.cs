using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Meetings;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Meetings;

public class MeetingDeleteEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.DeleteAsync($"/api/meetings/{meeting.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_DeleteMeetings() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.DeleteAsync($"/api/meetings/{meeting.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_404_for_nonexistent_meeting() {
        var chapter = Builder.SeedChapter("C");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.DeleteMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.DeleteAsync($"/api/meetings/{Guid.NewGuid()}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Soft_deletes_meeting() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.DeleteMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.DeleteAsync($"/api/meetings/{meeting.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var persisted = Db.Meetings.First(m => m.Id == meeting.Id);
        await Assert.That(persisted.DeletedAt).IsNotNull();
    }

    [Test]
    public async Task Does_not_break_referential_integrity() {
        // Meeting with agenda items still references them after soft-delete.
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        var item = Builder.SeedAgendaItem(meeting.Id, title: "TOP 1");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.DeleteMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.DeleteAsync($"/api/meetings/{meeting.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var stillThere = Db.AgendaItems.FirstOrDefault(a => a.Id == item.Id);
        await Assert.That(stillThere).IsNotNull();
    }
}
