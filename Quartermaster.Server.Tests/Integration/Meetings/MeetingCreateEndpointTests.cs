using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Meetings;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Meetings;

public class MeetingCreateEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        var chapter = Builder.SeedChapter("C");
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync("/api/meetings", new MeetingCreateRequest {
            ChapterId = chapter.Id,
            Title = "Test"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_CreateMeetings() {
        var chapter = Builder.SeedChapter("C");
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/meetings", new MeetingCreateRequest {
            ChapterId = chapter.Id,
            Title = "Test"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Creates_meeting_in_Draft_status() {
        var chapter = Builder.SeedChapter("C");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.CreateMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/meetings", new MeetingCreateRequest {
            ChapterId = chapter.Id,
            Title = "March Vorstand",
            Visibility = MeetingVisibility.Private
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<MeetingDTO>();
        await Assert.That(dto!.Status).IsEqualTo(MeetingStatus.Draft);
        await Assert.That(dto.Title).IsEqualTo("March Vorstand");
        var persisted = Db.Meetings.FirstOrDefault(m => m.Id == dto.Id);
        await Assert.That(persisted).IsNotNull();
    }

    [Test]
    public async Task Returns_400_when_title_empty() {
        var chapter = Builder.SeedChapter("C");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.CreateMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/meetings", new MeetingCreateRequest {
            ChapterId = chapter.Id,
            Title = ""
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Returns_400_when_chapter_id_empty() {
        var chapter = Builder.SeedChapter("C");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.CreateMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/meetings", new MeetingCreateRequest {
            ChapterId = Guid.Empty,
            Title = "Test"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Respects_chapter_scoping_when_creating_in_other_chapter() {
        var chapterA = Builder.SeedChapter("A");
        var chapterB = Builder.SeedChapter("B");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterA.Id] = new[] { PermissionIdentifier.CreateMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/meetings", new MeetingCreateRequest {
            ChapterId = chapterB.Id,
            Title = "X"
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }
}
