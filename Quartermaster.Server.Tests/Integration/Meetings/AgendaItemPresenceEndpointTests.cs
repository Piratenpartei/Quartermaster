using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.Meetings;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Meetings;

public class AgendaItemPresenceEndpointTests : IntegrationTestBase {
    private (Guid chapterId, Guid meetingId, Guid itemId) SeedPresenceItem(
        MeetingStatus status = MeetingStatus.InProgress,
        AgendaItemType itemType = AgendaItemType.Presence) {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: status);
        var item = Builder.SeedAgendaItem(meeting.Id, itemType: itemType);
        return (chapter.Id, meeting.Id, item.Id);
    }

    [Test]
    public async Task Returns_401_when_anonymous() {
        var (_, meetingId, itemId) = SeedPresenceItem();
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meetingId}/agenda/{itemId}/presence",
            new AgendaItemPresenceRequest { MeetingId = meetingId, ItemId = itemId, UserId = Guid.NewGuid(), Present = true });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_EditMeetings() {
        var (_, meetingId, itemId) = SeedPresenceItem();
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meetingId}/agenda/{itemId}/presence",
            new AgendaItemPresenceRequest { MeetingId = meetingId, ItemId = itemId, UserId = Guid.NewGuid(), Present = true });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_400_when_meeting_not_in_progress() {
        var (chapterId, meetingId, itemId) = SeedPresenceItem(status: MeetingStatus.Scheduled);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterId] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meetingId}/agenda/{itemId}/presence",
            new AgendaItemPresenceRequest { MeetingId = meetingId, ItemId = itemId, UserId = Guid.NewGuid(), Present = true });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Returns_400_when_item_not_presence_type() {
        var (chapterId, meetingId, itemId) = SeedPresenceItem(itemType: AgendaItemType.Discussion);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterId] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meetingId}/agenda/{itemId}/presence",
            new AgendaItemPresenceRequest { MeetingId = meetingId, ItemId = itemId, UserId = Guid.NewGuid(), Present = true });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Adds_user_to_present_set() {
        var (chapterId, meetingId, itemId) = SeedPresenceItem();
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterId] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var targetUserId = Guid.NewGuid();
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meetingId}/agenda/{itemId}/presence",
            new AgendaItemPresenceRequest { MeetingId = meetingId, ItemId = itemId, UserId = targetUserId, Present = true });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var updated = Db.AgendaItems.First(a => a.Id == itemId);
        var present = JsonSerializer.Deserialize<List<string>>(updated.Resolution!)!;
        await Assert.That(present.Contains(targetUserId.ToString())).IsTrue();
    }

    [Test]
    public async Task Removes_user_from_present_set_when_present_false() {
        var (chapterId, meetingId, itemId) = SeedPresenceItem();
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterId] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var targetUserId = Guid.NewGuid();

        await client.PostAsJsonAsync(
            $"/api/meetings/{meetingId}/agenda/{itemId}/presence",
            new AgendaItemPresenceRequest { MeetingId = meetingId, ItemId = itemId, UserId = targetUserId, Present = true });

        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meetingId}/agenda/{itemId}/presence",
            new AgendaItemPresenceRequest { MeetingId = meetingId, ItemId = itemId, UserId = targetUserId, Present = false });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var updated = Db.AgendaItems.First(a => a.Id == itemId);
        var present = JsonSerializer.Deserialize<List<string>>(updated.Resolution!)!;
        await Assert.That(present.Contains(targetUserId.ToString())).IsFalse();
    }

    [Test]
    public async Task Recovers_from_corrupted_resolution_json() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: MeetingStatus.InProgress);
        var item = Builder.SeedAgendaItem(meeting.Id, itemType: AgendaItemType.Presence);
        Db.AgendaItems.Where(a => a.Id == item.Id)
            .Set(a => a.Resolution, "not valid json")
            .Update();

        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var targetUserId = Guid.NewGuid();
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/{item.Id}/presence",
            new AgendaItemPresenceRequest { MeetingId = meeting.Id, ItemId = item.Id, UserId = targetUserId, Present = true });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var updated = Db.AgendaItems.First(a => a.Id == item.Id);
        var present = JsonSerializer.Deserialize<List<string>>(updated.Resolution!)!;
        await Assert.That(present.Count).IsEqualTo(1);
        await Assert.That(present[0]).IsEqualTo(targetUserId.ToString());
    }
}
