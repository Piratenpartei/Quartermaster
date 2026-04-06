using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Meetings;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Meetings;

public class MeetingListEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Anonymous_sees_only_public_meetings() {
        var chapter = Builder.SeedChapter("C");
        Builder.SeedMeeting(chapter.Id, title: "Public", visibility: MeetingVisibility.Public);
        Builder.SeedMeeting(chapter.Id, title: "Private", visibility: MeetingVisibility.Private);
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/meetings");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<MeetingListResponse>();
        await Assert.That(result!.Items.Count).IsEqualTo(1);
        await Assert.That(result.Items[0].Title).IsEqualTo("Public");
    }

    [Test]
    public async Task User_with_view_permission_sees_public_meetings_in_chapter() {
        var chapter = Builder.SeedChapter("C");
        Builder.SeedMeeting(chapter.Id, title: "Public", visibility: MeetingVisibility.Public);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.ViewMeetings } });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/meetings");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<MeetingListResponse>();
        await Assert.That(result!.Items.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Officer_of_child_chapter_does_not_see_parent_private_meeting() {
        var chain = Builder.SeedChapterHierarchy("Parent", "Child");
        Builder.SeedMeeting(chain[0].Id, title: "ParentPrivate", visibility: MeetingVisibility.Private);
        var (user, token) = Builder.SeedAuthenticatedUser();
        // Trigger factory startup so system roles are seeded.
        using var bootClient = AuthenticatedClient(token);
        var _unused = await bootClient.GetAsync("/api/meetings");
        var officerRole = Db.Roles.First(r => r.Identifier == PermissionIdentifier.SystemRole.ChapterOfficer);
        Builder.AssignRoleToUser(user.Id, officerRole.Id, chain[1].Id);
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/meetings");
        var result = await response.Content.ReadFromJsonAsync<MeetingListResponse>();
        await Assert.That(result!.Items.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Delegate_sees_chapter_private_meeting() {
        var chapter = Builder.SeedChapter("C");
        Builder.SeedMeeting(chapter.Id, title: "Private", visibility: MeetingVisibility.Private);
        var (user, token) = Builder.SeedAuthenticatedUser();
        using var bootClient = AuthenticatedClient(token);
        var _unused = await bootClient.GetAsync("/api/meetings");
        var delegateRole = Db.Roles.First(r => r.Identifier == PermissionIdentifier.SystemRole.GeneralChapterDelegate);
        Builder.AssignRoleToUser(user.Id, delegateRole.Id, chapter.Id);
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/meetings");
        var result = await response.Content.ReadFromJsonAsync<MeetingListResponse>();
        await Assert.That(result!.Items.Count).IsEqualTo(1);
        await Assert.That(result.Items[0].Title).IsEqualTo("Private");
    }

    [Test]
    public async Task Filters_by_status() {
        var chapter = Builder.SeedChapter("C");
        Builder.SeedMeeting(chapter.Id, title: "Draft", status: MeetingStatus.Draft, visibility: MeetingVisibility.Public);
        Builder.SeedMeeting(chapter.Id, title: "Scheduled", status: MeetingStatus.Scheduled, visibility: MeetingVisibility.Public);
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/meetings?Status={(int)MeetingStatus.Scheduled}");
        var result = await response.Content.ReadFromJsonAsync<MeetingListResponse>();
        await Assert.That(result!.Items.Count).IsEqualTo(1);
        await Assert.That(result.Items[0].Title).IsEqualTo("Scheduled");
    }

    [Test]
    public async Task Paginated_response_respects_page_size() {
        var chapter = Builder.SeedChapter("C");
        for (int i = 0; i < 5; i++) {
            Builder.SeedMeeting(chapter.Id, title: $"M{i}", visibility: MeetingVisibility.Public);
        }
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/meetings?Page=1&PageSize=2");
        var result = await response.Content.ReadFromJsonAsync<MeetingListResponse>();
        await Assert.That(result!.Items.Count).IsEqualTo(2);
        await Assert.That(result.TotalCount).IsEqualTo(5);
    }

    [Test]
    public async Task Rejects_page_size_over_100() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/meetings?Page=1&PageSize=500");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}
