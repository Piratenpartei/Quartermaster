using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Quartermaster.Api;
using Quartermaster.Api.Meetings;
using Quartermaster.Data.Collab;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Meetings;

/// <summary>
/// Exercises the collaborative-editing methods on <c>MeetingHub</c>: loading
/// the initial document, relaying updates between two clients, and
/// persisting a snapshot.
/// </summary>
public class MeetingHubCollabTests : IntegrationTestBase {
    private HubConnection BuildConnection(string token) {
        var handler = Factory.Server.CreateHandler();
        return new HubConnectionBuilder()
            .WithUrl(new Uri(Factory.Server.BaseAddress, "/hubs/meeting"), options => {
                options.HttpMessageHandlerFactory = _ => handler;
                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();
    }

    [Test]
    public async Task LoadDocument_assigns_distinct_colors_to_different_users() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: MeetingStatus.InProgress);
        var item = Builder.SeedAgendaItem(meeting.Id);
        var (_, tokenA) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });
        var (_, tokenB) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });

        await using var connA = BuildConnection(tokenA);
        await connA.StartAsync();
        var snapA = await connA.InvokeAsync<CollabDocumentSnapshot>("LoadDocument", item.Id);

        await using var connB = BuildConnection(tokenB);
        await connB.StartAsync();
        var snapB = await connB.InvokeAsync<CollabDocumentSnapshot>("LoadDocument", item.Id);

        await Assert.That(snapA.AssignedColor).IsNotEqualTo("");
        await Assert.That(snapB.AssignedColor).IsNotEqualTo("");
        await Assert.That(snapA.AssignedColor).IsNotEqualTo(snapB.AssignedColor);
    }

    [Test]
    public async Task LoadDocument_reuses_existing_color_on_reconnect() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: MeetingStatus.InProgress);
        var item = Builder.SeedAgendaItem(meeting.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });

        await using var conn1 = BuildConnection(token);
        await conn1.StartAsync();
        var first = await conn1.InvokeAsync<CollabDocumentSnapshot>("LoadDocument", item.Id);

        // Same user reconnects — should get the same color.
        await using var conn2 = BuildConnection(token);
        await conn2.StartAsync();
        var second = await conn2.InvokeAsync<CollabDocumentSnapshot>("LoadDocument", item.Id);

        await Assert.That(second.AssignedColor).IsEqualTo(first.AssignedColor);
    }

    [Test]
    public async Task LoadDocument_returns_empty_snapshot_for_new_agenda_item() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: MeetingStatus.InProgress);
        var item = Builder.SeedAgendaItem(meeting.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });

        await using var conn = BuildConnection(token);
        await conn.StartAsync();

        var snapshot = await conn.InvokeAsync<CollabDocumentSnapshot>("LoadDocument", item.Id);
        await Assert.That(snapshot).IsNotNull();
        await Assert.That(snapshot.DocumentStateBase64).IsEqualTo("");
        await Assert.That(snapshot.CanEdit).IsTrue();
        await Assert.That(snapshot.SaveIntervalSeconds).IsGreaterThan(0);
    }

    [Test]
    public async Task SendUpdate_relays_bytes_to_other_clients_in_document_group() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: MeetingStatus.InProgress);
        var item = Builder.SeedAgendaItem(meeting.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });

        await using var listener = BuildConnection(token);
        var receivedTcs = new TaskCompletionSource<(Guid, string)>();
        listener.On<Guid, string>("ReceiveUpdate", (id, b64) => receivedTcs.TrySetResult((id, b64)));
        await listener.StartAsync();
        await listener.InvokeAsync<CollabDocumentSnapshot>("LoadDocument", item.Id);

        await using var sender = BuildConnection(token);
        await sender.StartAsync();
        await sender.InvokeAsync<CollabDocumentSnapshot>("LoadDocument", item.Id);

        var fakeUpdate = Convert.ToBase64String(Encoding.UTF8.GetBytes("fake-yjs-update"));
        await sender.InvokeAsync("SendUpdate", item.Id, fakeUpdate);

        var done = await Task.WhenAny(receivedTcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        await Assert.That(done == receivedTcs.Task).IsTrue();
        var (receivedId, receivedPayload) = await receivedTcs.Task;
        await Assert.That(receivedId).IsEqualTo(item.Id);
        await Assert.That(receivedPayload).IsEqualTo(fakeUpdate);
    }

    [Test]
    public async Task SaveSnapshot_persists_document_and_mirrors_to_agenda_notes() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: MeetingStatus.InProgress);
        var item = Builder.SeedAgendaItem(meeting.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });

        await using var conn = BuildConnection(token);
        await conn.StartAsync();
        await conn.InvokeAsync<CollabDocumentSnapshot>("LoadDocument", item.Id);

        var docBytes = Convert.ToBase64String(Encoding.UTF8.GetBytes("fake-yjs-state"));
        await conn.InvokeAsync("SaveSnapshot", new CollabSnapshotRequest {
            AgendaItemId = item.Id,
            DocumentStateBase64 = docBytes,
            PlainText = "Saved notes content",
            KnownAuthors = new Dictionary<string, CollabAuthorInfo>()
        });

        // Verify the CollabDocument row was created.
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<CollabDocumentRepository>();
        var doc = repo.Get("AgendaItem", item.Id);
        await Assert.That(doc).IsNotNull();
        await Assert.That(doc!.PlainText).IsEqualTo("Saved notes content");
        await Assert.That(doc.DocumentState).IsEqualTo(docBytes);

        // Verify the agenda item's Notes column was also updated.
        var agendaRepo = scope.ServiceProvider.GetRequiredService<Quartermaster.Data.Meetings.AgendaItemRepository>();
        var refreshed = agendaRepo.Get(item.Id);
        await Assert.That(refreshed!.Notes).IsEqualTo("Saved notes content");
    }

    [Test]
    public async Task LoadDocument_on_completed_meeting_returns_canEdit_false() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: MeetingStatus.Completed);
        var item = Builder.SeedAgendaItem(meeting.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });

        await using var conn = BuildConnection(token);
        await conn.StartAsync();

        var snapshot = await conn.InvokeAsync<CollabDocumentSnapshot>("LoadDocument", item.Id);
        await Assert.That(snapshot).IsNotNull();
        await Assert.That(snapshot.CanEdit).IsFalse();
    }

    [Test]
    public async Task SendUpdate_on_completed_meeting_is_forbidden() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: MeetingStatus.Completed);
        var item = Builder.SeedAgendaItem(meeting.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });

        await using var conn = BuildConnection(token);
        await conn.StartAsync();
        await conn.InvokeAsync<CollabDocumentSnapshot>("LoadDocument", item.Id);

        var fakeUpdate = Convert.ToBase64String(Encoding.UTF8.GetBytes("fake"));
        await Assert.ThrowsAsync<HubException>(async () =>
            await conn.InvokeAsync("SendUpdate", item.Id, fakeUpdate));
    }

    [Test]
    public async Task LoadDocument_returns_existing_snapshot_after_save() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: MeetingStatus.InProgress);
        var item = Builder.SeedAgendaItem(meeting.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });

        await using var conn = BuildConnection(token);
        await conn.StartAsync();
        await conn.InvokeAsync<CollabDocumentSnapshot>("LoadDocument", item.Id);

        var docBytes = Convert.ToBase64String(Encoding.UTF8.GetBytes("fake-yjs-state-round-trip"));
        await conn.InvokeAsync("SaveSnapshot", new CollabSnapshotRequest {
            AgendaItemId = item.Id,
            DocumentStateBase64 = docBytes,
            PlainText = "Round-trip plain text",
            KnownAuthors = new Dictionary<string, CollabAuthorInfo>()
        });

        // A second LoadDocument call should return the saved state.
        await using var conn2 = BuildConnection(token);
        await conn2.StartAsync();
        var reloaded = await conn2.InvokeAsync<CollabDocumentSnapshot>("LoadDocument", item.Id);
        await Assert.That(reloaded.DocumentStateBase64).IsEqualTo(docBytes);
        await Assert.That(reloaded.PlainText).IsEqualTo("Round-trip plain text");
    }
}
