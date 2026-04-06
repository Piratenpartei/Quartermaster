using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Meetings;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Meetings;

public class MeetingUpdateEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PutAsJsonAsync($"/api/meetings/{meeting.Id}", new MeetingUpdateRequest {
            Id = meeting.Id,
            Title = "Updated"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_EditMeetings() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/meetings/{meeting.Id}", new MeetingUpdateRequest {
            Id = meeting.Id,
            Title = "Updated"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_404_for_nonexistent_meeting() {
        var chapter = Builder.SeedChapter("C");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/meetings/{Guid.NewGuid()}", new MeetingUpdateRequest {
            Id = Guid.NewGuid(),
            Title = "Updated"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Updates_title_and_visibility() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, title: "Old", visibility: MeetingVisibility.Private);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/meetings/{meeting.Id}", new MeetingUpdateRequest {
            Id = meeting.Id,
            Title = "New",
            Visibility = MeetingVisibility.Public
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updated = Db.Meetings.First(m => m.Id == meeting.Id);
        await Assert.That(updated.Title).IsEqualTo("New");
        await Assert.That(updated.Visibility).IsEqualTo(MeetingVisibility.Public);
    }

    [Test]
    public async Task Returns_400_when_title_empty() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/meetings/{meeting.Id}", new MeetingUpdateRequest {
            Id = meeting.Id,
            Title = ""
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}
