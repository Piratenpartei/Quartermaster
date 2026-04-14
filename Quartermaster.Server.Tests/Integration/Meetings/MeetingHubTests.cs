using System;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Quartermaster.Api;
using Quartermaster.Api.Meetings;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Meetings;

/// <summary>
/// Verifies the SignalR meeting hub relays notifications from mutation
/// endpoints to connected clients in the meeting's group.
/// </summary>
public class MeetingHubTests : IntegrationTestBase {
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
    public async Task JoinMeeting_rejects_unknown_meeting() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        await using var conn = BuildConnection(token);
        await conn.StartAsync();

        await Assert.ThrowsAsync<HubException>(async () =>
            await conn.InvokeAsync("JoinMeeting", Guid.NewGuid()));
    }

    [Test]
    public async Task Clients_in_same_meeting_group_receive_agenda_item_changed() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: MeetingStatus.InProgress);
        var item = Builder.SeedAgendaItem(meeting.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });

        await using var listener = BuildConnection(token);
        var tcs = new TaskCompletionSource<AgendaItemChangedMessage>();
        listener.On<AgendaItemChangedMessage>(MeetingHubMethods.AgendaItemChanged, msg => {
            tcs.TrySetResult(msg);
        });
        await listener.StartAsync();
        await listener.InvokeAsync("JoinMeeting", meeting.Id);

        // A separate HTTP client triggers an agenda-item mutation while the
        // listener is joined to the meeting's group. The hub should broadcast
        // an AgendaItemChanged notification, which the listener receives.
        using var http = await AuthenticatedClientWithCsrfAsync(token);
        var response = await http.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/agenda/{item.Id}/start", new { });
        response.EnsureSuccessStatusCode();

        var received = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        await Assert.That(received == tcs.Task).IsTrue();
        var msg = await tcs.Task;
        await Assert.That(msg.MeetingId).IsEqualTo(meeting.Id);
        await Assert.That(msg.AgendaItemId).IsEqualTo(item.Id);
        await Assert.That(msg.Reason).IsEqualTo("started");
    }

    [Test]
    public async Task Clients_in_same_meeting_group_receive_status_changed() {
        var chapter = Builder.SeedChapter("C");
        var meeting = Builder.SeedMeeting(chapter.Id, status: MeetingStatus.Scheduled);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditMeetings } });

        await using var listener = BuildConnection(token);
        var tcs = new TaskCompletionSource<MeetingStatusChangedMessage>();
        listener.On<MeetingStatusChangedMessage>(MeetingHubMethods.MeetingStatusChanged, msg => {
            tcs.TrySetResult(msg);
        });
        await listener.StartAsync();
        await listener.InvokeAsync("JoinMeeting", meeting.Id);

        using var http = await AuthenticatedClientWithCsrfAsync(token);
        var response = await http.PutAsJsonAsync($"/api/meetings/{meeting.Id}/status",
            new MeetingStatusUpdateRequest { Id = meeting.Id, Status = MeetingStatus.InProgress });
        response.EnsureSuccessStatusCode();

        var received = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        await Assert.That(received == tcs.Task).IsTrue();
        var msg = await tcs.Task;
        await Assert.That(msg.MeetingId).IsEqualTo(meeting.Id);
        await Assert.That(msg.NewStatus).IsEqualTo(MeetingStatus.InProgress);
    }
}
