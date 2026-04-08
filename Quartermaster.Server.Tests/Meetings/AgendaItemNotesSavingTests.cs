using System;
using System.Net;
using System.Net.Http.Json;
using System.Linq;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Meetings;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Meetings;

/// <summary>
/// Tests that agenda item notes are actually persisted and survive
/// item completion + subsequent item switches.
/// </summary>
public class AgendaItemNotesSavingTests : IntegrationTestBase {
    [Test]
    public async Task Notes_saved_via_notes_endpoint_persist_in_DB() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: MeetingStatus.InProgress);
        var item = Builder.SeedAgendaItem(meeting.Id, title: "TOP 1");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await client.PutAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/{item.Id}/notes",
            new { MeetingId = meeting.Id, ItemId = item.Id, Notes = "These are my notes" });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var saved = Db.AgendaItems.First(a => a.Id == item.Id);
        await Assert.That(saved.Notes).IsEqualTo("These are my notes");
    }

    [Test]
    public async Task Notes_survive_after_completing_the_item() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: MeetingStatus.InProgress);
        var item = Builder.SeedAgendaItem(meeting.Id, title: "TOP 1");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        // Save notes
        await client.PutAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/{item.Id}/notes",
            new { MeetingId = meeting.Id, ItemId = item.Id, Notes = "Important discussion" });

        // Complete the item
        await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/{item.Id}/complete", new { });

        // Verify notes are still there
        var saved = Db.AgendaItems.First(a => a.Id == item.Id);
        await Assert.That(saved.Notes).IsEqualTo("Important discussion");
        await Assert.That(saved.CompletedAt).IsNotNull();
    }

    [Test]
    public async Task Notes_survive_after_starting_a_different_item() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: MeetingStatus.InProgress);
        var item1 = Builder.SeedAgendaItem(meeting.Id, title: "TOP 1", sortOrder: 0);
        var item2 = Builder.SeedAgendaItem(meeting.Id, title: "TOP 2", sortOrder: 1);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        // Start and write notes on item 1
        await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/{item1.Id}/start", new { });
        await client.PutAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/{item1.Id}/notes",
            new { MeetingId = meeting.Id, ItemId = item1.Id, Notes = "Item 1 notes" });

        // Start item 2 (which auto-completes item 1)
        await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/{item2.Id}/start", new { });

        // Item 1's notes must still be there
        var saved1 = Db.AgendaItems.First(a => a.Id == item1.Id);
        await Assert.That(saved1.Notes).IsEqualTo("Item 1 notes");
        await Assert.That(saved1.CompletedAt).IsNotNull();
    }

    [Test]
    public async Task Reopened_item_retains_its_notes() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: MeetingStatus.InProgress);
        var item = Builder.SeedAgendaItem(meeting.Id, title: "TOP 1");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        // Save notes, complete, then reopen
        await client.PutAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/{item.Id}/notes",
            new { MeetingId = meeting.Id, ItemId = item.Id, Notes = "Keep me" });
        await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/{item.Id}/complete", new { });
        await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/{item.Id}/reopen", new { });

        var saved = Db.AgendaItems.First(a => a.Id == item.Id);
        await Assert.That(saved.Notes).IsEqualTo("Keep me");
        await Assert.That(saved.CompletedAt).IsNull();
    }
}
