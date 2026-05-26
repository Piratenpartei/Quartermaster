using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Meetings;
using Quartermaster.Api.Motions;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Meetings;

public class AgendaItemImportMotionsEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/import-motions", new { meetingId = meeting.Id });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_EditMeetings() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/import-motions", new { meetingId = meeting.Id });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_404_when_meeting_missing() {
        var chapter = Builder.SeedChapter("C");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var fake = Guid.NewGuid();
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{fake}/agenda/import-motions", new { meetingId = fake });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Imports_pending_motions_as_agenda_items_under_parent() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        var section = Builder.SeedAgendaItem(meeting.Id, itemType: AgendaItemType.Section, title: "Motions");
        var m1 = Builder.SeedMotion(chapter.Id, title: "M1");
        var m2 = Builder.SeedMotion(chapter.Id, title: "M2");
        Builder.SeedMotion(chapter.Id, title: "Approved", status: MotionApprovalStatus.Approved);

        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/import-motions",
            new { meetingId = meeting.Id, parentId = section.Id });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        await Assert.That(doc.RootElement.GetProperty("imported").GetInt32()).IsEqualTo(2);

        var items = Db.AgendaItems.Where(a => a.MeetingId == meeting.Id && a.ParentId == section.Id).ToList();
        await Assert.That(items.Count).IsEqualTo(2);
        await Assert.That(items.All(i => i.ItemType == AgendaItemType.Motion)).IsTrue();
        await Assert.That(items.Any(i => i.MotionId == m1.Id) && items.Any(i => i.MotionId == m2.Id)).IsTrue();
    }

    [Test]
    public async Task Skips_motions_already_linked_in_meeting() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id);
        var existingMotion = Builder.SeedMotion(chapter.Id, title: "Existing");
        Builder.SeedAgendaItem(meeting.Id, itemType: AgendaItemType.Motion, motionId: existingMotion.Id);
        Builder.SeedMotion(chapter.Id, title: "New1");
        Builder.SeedMotion(chapter.Id, title: "New2");

        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/import-motions", new { meetingId = meeting.Id });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        await Assert.That(doc.RootElement.GetProperty("imported").GetInt32()).IsEqualTo(2);
    }
}
