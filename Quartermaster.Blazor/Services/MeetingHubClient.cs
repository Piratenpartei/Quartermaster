using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Quartermaster.Api.Meetings;

namespace Quartermaster.Blazor.Services;

/// <summary>
/// Wraps the SignalR <see cref="HubConnection"/> for the meeting hub. Scoped per
/// circuit/user. Handles lazy connect, auto-reconnect, and exposes simple events
/// so pages don't need to know about SignalR.
/// </summary>
public class MeetingHubClient : IAsyncDisposable {
    private readonly NavigationManager _navigation;
    private readonly AuthService _authService;
    private HubConnection? _connection;
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    public event Action<AgendaItemChangedMessage>? AgendaItemChanged;
    public event Action<MeetingStatusChangedMessage>? MeetingStatusChanged;
    public event Action<PresenceChangedMessage>? PresenceChanged;

    /// <summary>Fires when the server relays a Yjs update from another editor. Args: (agendaItemId, updateBase64).</summary>
    public event Action<Guid, string>? UpdateReceived;

    /// <summary>Fires when the server relays a Yjs awareness update from another editor. Args: (agendaItemId, awarenessBase64).</summary>
    public event Action<Guid, string>? AwarenessReceived;

    /// <summary>Fires whenever the hub connection state changes. Argument: true when fully connected, false during reconnect/closed states.</summary>
    public event Action<bool>? ConnectionStateChanged;

    public MeetingHubClient(NavigationManager navigation, AuthService authService) {
        _navigation = navigation;
        _authService = authService;
    }

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    private async Task EnsureConnectedAsync() {
        if (_connection != null && _connection.State == HubConnectionState.Connected)
            return;

        await _connectLock.WaitAsync();
        try {
            if (_connection != null && _connection.State == HubConnectionState.Connected)
                return;

            if (_connection == null) {
                var hubUrl = _navigation.ToAbsoluteUri("/hubs/meeting");
                _connection = new HubConnectionBuilder()
                    .WithUrl(hubUrl, options => {
                        options.AccessTokenProvider = () => Task.FromResult<string?>(_authService.Token);
                    })
                    .WithAutomaticReconnect()
                    .Build();

                _connection.On<AgendaItemChangedMessage>(
                    MeetingHubMethods.AgendaItemChanged,
                    msg => AgendaItemChanged?.Invoke(msg));
                _connection.On<MeetingStatusChangedMessage>(
                    MeetingHubMethods.MeetingStatusChanged,
                    msg => MeetingStatusChanged?.Invoke(msg));
                _connection.On<PresenceChangedMessage>(
                    MeetingHubMethods.PresenceChanged,
                    msg => PresenceChanged?.Invoke(msg));
                _connection.On<Guid, string>(
                    MeetingHubMethods.ReceiveUpdate,
                    (id, update) => UpdateReceived?.Invoke(id, update));
                _connection.On<Guid, string>(
                    MeetingHubMethods.ReceiveAwareness,
                    (id, awareness) => AwarenessReceived?.Invoke(id, awareness));

                _connection.Reconnecting += _ => {
                    ConnectionStateChanged?.Invoke(false);
                    return Task.CompletedTask;
                };
                _connection.Reconnected += _ => {
                    ConnectionStateChanged?.Invoke(true);
                    return Task.CompletedTask;
                };
                _connection.Closed += _ => {
                    ConnectionStateChanged?.Invoke(false);
                    return Task.CompletedTask;
                };
            }

            if (_connection.State == HubConnectionState.Disconnected)
                await _connection.StartAsync();
        } finally {
            _connectLock.Release();
        }
    }

    public async Task JoinMeetingAsync(Guid meetingId) {
        await EnsureConnectedAsync();
        if (_connection != null)
            await _connection.InvokeAsync("JoinMeeting", meetingId);
    }

    public async Task LeaveMeetingAsync(Guid meetingId) {
        if (_connection == null || _connection.State != HubConnectionState.Connected)
            return;
        await _connection.InvokeAsync("LeaveMeeting", meetingId);
    }

    public async Task<CollabDocumentSnapshot> LoadDocumentAsync(Guid agendaItemId) {
        await EnsureConnectedAsync();
        return await _connection!.InvokeAsync<CollabDocumentSnapshot>("LoadDocument", agendaItemId);
    }

    public async Task SendUpdateAsync(Guid agendaItemId, string updateBase64) {
        if (_connection == null || _connection.State != HubConnectionState.Connected)
            return;
        await _connection.InvokeAsync("SendUpdate", agendaItemId, updateBase64);
    }

    public async Task SendAwarenessAsync(Guid agendaItemId, string awarenessBase64) {
        if (_connection == null || _connection.State != HubConnectionState.Connected)
            return;
        await _connection.InvokeAsync("SendAwareness", agendaItemId, awarenessBase64);
    }

    public async Task SaveSnapshotAsync(CollabSnapshotRequest request) {
        if (_connection == null || _connection.State != HubConnectionState.Connected)
            return;
        await _connection.InvokeAsync("SaveSnapshot", request);
    }

    public async ValueTask DisposeAsync() {
        if (_connection != null) {
            await _connection.DisposeAsync();
            _connection = null;
        }
        _connectLock.Dispose();
    }
}
