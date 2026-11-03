using System;
using System.Collections.Generic;
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

    private async Task<MeetingDetailDTO> DetailFor(Guid meetingId, Guid chapterId, params string[] permissions) {
        var (_, token) = Builder.SeedAuthenticatedUser(chapterPermissions: new Dictionary<Guid, string[]> {
            [chapterId] = permissions
        });
        using var client = AuthenticatedClient(token);
        var dto = await client.GetFromJsonAsync<MeetingDetailDTO>($"/api/meetings/{meetingId}");
        return dto!;
    }

    [Test]
    public async Task View_only_user_gets_no_edit_flags() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, visibility: MeetingVisibility.Public);

        var dto = await DetailFor(meeting.Id, chapter.Id, PermissionIdentifier.ViewMeetings);

        await Assert.That(dto.CanEdit).IsFalse();
        await Assert.That(dto.CanDelete).IsFalse();
    }

    [Test]
    public async Task Edit_flags_follow_the_permissions() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, visibility: MeetingVisibility.Public);

        var edit = await DetailFor(meeting.Id, chapter.Id, PermissionIdentifier.EditMeetings);
        var delete = await DetailFor(meeting.Id, chapter.Id, PermissionIdentifier.DeleteMeetings);

        await Assert.That(edit.CanEdit).IsTrue();
        await Assert.That(edit.CanDelete).IsFalse();
        await Assert.That(delete.CanDelete).IsTrue();
        await Assert.That(delete.CanEdit).IsFalse();
    }

    [Test]
    public async Task Anonymous_gets_no_edit_flags() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, visibility: MeetingVisibility.Public);
        using var client = AnonymousClient();

        var dto = await client.GetFromJsonAsync<MeetingDetailDTO>($"/api/meetings/{meeting.Id}");

        await Assert.That(dto!.CanEdit).IsFalse();
        await Assert.That(dto.CanDelete).IsFalse();
    }
}
