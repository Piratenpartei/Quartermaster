using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Meetings;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Meetings;

public class AgendaItemAddEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/agenda", new AgendaItemCreateRequest {
            MeetingId = meeting.Id,
            Title = "TOP 1",
            ItemType = AgendaItemType.Discussion
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_EditMeetings() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/agenda", new AgendaItemCreateRequest {
            MeetingId = meeting.Id,
            Title = "TOP 1",
            ItemType = AgendaItemType.Discussion
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_404_when_meeting_not_found() {
        var chapter = Builder.SeedChapter("C");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var fakeId = Guid.NewGuid();
        var response = await client.PostAsJsonAsync($"/api/meetings/{fakeId}/agenda", new AgendaItemCreateRequest {
            MeetingId = fakeId,
            Title = "TOP",
            ItemType = AgendaItemType.Discussion
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Creates_root_agenda_item() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/agenda", new AgendaItemCreateRequest {
            MeetingId = meeting.Id,
            Title = "TOP 1",
            ItemType = AgendaItemType.Discussion
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<AgendaItemDTO>();
        await Assert.That(dto!.Title).IsEqualTo("TOP 1");
        await Assert.That(dto.ParentId).IsNull();
    }

    [Test]
    public async Task Creates_subitem_with_parent_id() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        var parent = Builder.SeedAgendaItem(meeting.Id, title: "Parent");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/agenda", new AgendaItemCreateRequest {
            MeetingId = meeting.Id,
            ParentId = parent.Id,
            Title = "Sub",
            ItemType = AgendaItemType.Discussion
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<AgendaItemDTO>();
        await Assert.That(dto!.ParentId).IsEqualTo(parent.Id);
    }

    [Test]
    public async Task Returns_400_when_parent_in_different_meeting() {
        var chapter = Builder.SeedChapter("C");
        var meetingA = Builder.SeedMeeting(chapter.Id, title: "A");
        var meetingB = Builder.SeedMeeting(chapter.Id, title: "B");
        var parentInB = Builder.SeedAgendaItem(meetingB.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync($"/api/meetings/{meetingA.Id}/agenda", new AgendaItemCreateRequest {
            MeetingId = meetingA.Id,
            ParentId = parentInB.Id,
            Title = "X",
            ItemType = AgendaItemType.Discussion
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Returns_400_when_depth_exceeds_max() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        var root = Builder.SeedAgendaItem(meeting.Id, title: "Root");
        var level2 = Builder.SeedAgendaItem(meeting.Id, parentId: root.Id, title: "Lvl2");
        var level3 = Builder.SeedAgendaItem(meeting.Id, parentId: level2.Id, title: "Lvl3");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        // Attempt to add level4 (under level3, which is already at depth 3)
        var response = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/agenda", new AgendaItemCreateRequest {
            MeetingId = meeting.Id,
            ParentId = level3.Id,
            Title = "Lvl4",
            ItemType = AgendaItemType.Discussion
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Returns_400_when_motion_type_without_motion_id() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/agenda", new AgendaItemCreateRequest {
            MeetingId = meeting.Id,
            Title = "Motion TOP",
            ItemType = AgendaItemType.Motion,
            MotionId = null
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Returns_400_when_motion_in_different_chapter() {
        var chapterA = Builder.SeedChapter("A");
        var chapterB = Builder.SeedChapter("B");
        var meeting = Builder.SeedMeeting(chapterA.Id);
        var motionInB = Builder.SeedMotion(chapterB.Id, title: "B motion");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterA.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/agenda", new AgendaItemCreateRequest {
            MeetingId = meeting.Id,
            Title = "Motion TOP",
            ItemType = AgendaItemType.Motion,
            MotionId = motionInB.Id
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}
