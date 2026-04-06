using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Meetings;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Meetings;

public class AgendaItemMoveEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        var item = Builder.SeedAgendaItem(meeting.Id);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/{item.Id}/move",
            new AgendaItemMoveRequest {
                MeetingId = meeting.Id,
                ItemId = item.Id,
                NewParentId = null
            });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_EditMeetings() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        var item = Builder.SeedAgendaItem(meeting.Id);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/{item.Id}/move",
            new AgendaItemMoveRequest {
                MeetingId = meeting.Id,
                ItemId = item.Id,
                NewParentId = null
            });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Repositions_item_to_new_parent() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        var parentA = Builder.SeedAgendaItem(meeting.Id, title: "A");
        var parentB = Builder.SeedAgendaItem(meeting.Id, title: "B");
        var child = Builder.SeedAgendaItem(meeting.Id, parentId: parentA.Id, title: "Child");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/{child.Id}/move",
            new AgendaItemMoveRequest {
                MeetingId = meeting.Id,
                ItemId = child.Id,
                NewParentId = parentB.Id
            });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updated = Db.AgendaItems.First(a => a.Id == child.Id);
        await Assert.That(updated.ParentId).IsEqualTo(parentB.Id);
    }

    [Test]
    public async Task Returns_400_on_cycle() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        var root = Builder.SeedAgendaItem(meeting.Id, title: "Root");
        var child = Builder.SeedAgendaItem(meeting.Id, parentId: root.Id, title: "Child");
        // Try to make root's parent = child → cycle.
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/{root.Id}/move",
            new AgendaItemMoveRequest {
                MeetingId = meeting.Id,
                ItemId = root.Id,
                NewParentId = child.Id
            });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Returns_400_on_depth_overflow() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        var root = Builder.SeedAgendaItem(meeting.Id, title: "R");
        var l2 = Builder.SeedAgendaItem(meeting.Id, parentId: root.Id, title: "L2");
        var l3 = Builder.SeedAgendaItem(meeting.Id, parentId: l2.Id, title: "L3");
        var standalone = Builder.SeedAgendaItem(meeting.Id, title: "Standalone");
        // Moving standalone under l3 would make it depth 4 → too deep.
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/{standalone.Id}/move",
            new AgendaItemMoveRequest {
                MeetingId = meeting.Id,
                ItemId = standalone.Id,
                NewParentId = l3.Id
            });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Returns_400_when_new_parent_in_different_meeting() {
        var chapter = Builder.SeedChapter("C");
        var meetingA = Builder.SeedMeeting(chapter.Id, title: "A");
        var meetingB = Builder.SeedMeeting(chapter.Id, title: "B");
        var itemInA = Builder.SeedAgendaItem(meetingA.Id);
        var itemInB = Builder.SeedAgendaItem(meetingB.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meetingA.Id}/agenda/{itemInA.Id}/move",
            new AgendaItemMoveRequest {
                MeetingId = meetingA.Id,
                ItemId = itemInA.Id,
                NewParentId = itemInB.Id
            });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}
