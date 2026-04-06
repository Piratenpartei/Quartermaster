using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Meetings;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Meetings;

public class MeetingDetailEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Anonymous_can_view_public_meeting() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, visibility: MeetingVisibility.Public);
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/meetings/{meeting.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<MeetingDetailDTO>();
        await Assert.That(dto!.Id).IsEqualTo(meeting.Id);
    }

    [Test]
    public async Task Returns_404_for_private_meeting_to_anonymous() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, visibility: MeetingVisibility.Private);
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/meetings/{meeting.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Returns_404_for_private_meeting_to_inherited_viewer() {
        // Officer of parent chapter has inherited ViewMeetings but cannot see
        // private meeting of child chapter.
        var chain = Builder.SeedChapterHierarchy("Parent", "Child");
        var meeting = Builder.SeedMeeting(chain[1].Id, visibility: MeetingVisibility.Private);
        var (user, token) = Builder.SeedAuthenticatedUser();
        using var bootClient = AuthenticatedClient(token);
        var _unused = await bootClient.GetAsync("/api/meetings");
        var officerRole = Db.Roles.First(r => r.Identifier == PermissionIdentifier.SystemRole.ChapterOfficer);
        Builder.AssignRoleToUser(user.Id, officerRole.Id, chain[0].Id);
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/meetings/{meeting.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Returns_200_to_direct_officer_for_private_meeting() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, visibility: MeetingVisibility.Private);
        var (user, token) = Builder.SeedAuthenticatedUser();
        using var bootClient = AuthenticatedClient(token);
        var _unused = await bootClient.GetAsync("/api/meetings");
        var officerRole = Db.Roles.First(r => r.Identifier == PermissionIdentifier.SystemRole.ChapterOfficer);
        Builder.AssignRoleToUser(user.Id, officerRole.Id, chapter.Id);
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/meetings/{meeting.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Returns_404_for_nonexistent_meeting() {
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/meetings/{Guid.NewGuid()}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Includes_agenda_items_in_response() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, visibility: MeetingVisibility.Public);
        Builder.SeedAgendaItem(meeting.Id, title: "TOP 1", sortOrder: 0);
        Builder.SeedAgendaItem(meeting.Id, title: "TOP 2", sortOrder: 1);
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/meetings/{meeting.Id}");
        var dto = await response.Content.ReadFromJsonAsync<MeetingDetailDTO>();
        await Assert.That(dto!.AgendaItems.Count).IsEqualTo(2);
    }
}
