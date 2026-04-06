using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Meetings;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Meetings;

public class AgendaItemReorderEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        var item = Builder.SeedAgendaItem(meeting.Id, sortOrder: 0);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/{item.Id}/reorder",
            new AgendaItemReorderRequest {
                MeetingId = meeting.Id,
                ItemId = item.Id,
                Direction = 1
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
            $"/api/meetings/{meeting.Id}/agenda/{item.Id}/reorder",
            new AgendaItemReorderRequest {
                MeetingId = meeting.Id,
                ItemId = item.Id,
                Direction = 1
            });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_404_when_meeting_not_found() {
        var chapter = Builder.SeedChapter("C");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var fakeM = Guid.NewGuid();
        var fakeI = Guid.NewGuid();
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{fakeM}/agenda/{fakeI}/reorder",
            new AgendaItemReorderRequest {
                MeetingId = fakeM,
                ItemId = fakeI,
                Direction = 1
            });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Returns_400_for_invalid_direction() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        var item = Builder.SeedAgendaItem(meeting.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/{item.Id}/reorder",
            new AgendaItemReorderRequest {
                MeetingId = meeting.Id,
                ItemId = item.Id,
                Direction = 5
            });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Swaps_sort_order_when_moving_down() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        var itemA = Builder.SeedAgendaItem(meeting.Id, title: "A", sortOrder: 0);
        var itemB = Builder.SeedAgendaItem(meeting.Id, title: "B", sortOrder: 1);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/{itemA.Id}/reorder",
            new AgendaItemReorderRequest {
                MeetingId = meeting.Id,
                ItemId = itemA.Id,
                Direction = 1
            });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updatedA = Db.AgendaItems.First(a => a.Id == itemA.Id);
        var updatedB = Db.AgendaItems.First(a => a.Id == itemB.Id);
        await Assert.That(updatedA.SortOrder).IsEqualTo(1);
        await Assert.That(updatedB.SortOrder).IsEqualTo(0);
    }
}
