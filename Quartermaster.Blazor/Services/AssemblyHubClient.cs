using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Quartermaster.Api.AssemblyAssistant;

namespace Quartermaster.Blazor.Services;

/// <summary>
/// Wraps the SignalR connection to the assembly hub. Reconnects without giving up (the overlay runs all day as an OBS
/// source) and joins the console or overlay group again after every reconnect, since groups do not survive a reconnect.
/// </summary>
public class AssemblyHubClient : IAsyncDisposable {
    private readonly NavigationManager _navigation;
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private HubConnection? connection;
    private Guid? consoleEventId;
    private Guid? overlayEventId;

    public bool IsConnected => connection?.State == HubConnectionState.Connected;

    public event Action<AssemblySnapshotDTO>? SnapshotChanged;
    public event Action<AssemblyOverlayDTO>? OverlayChanged;
    public event Action? SessionEnded;
    public event Action<AssemblyConsoleJoinDTO>? ConsoleRejoined;
    public event Action<bool>? ConnectionStateChanged;

    public AssemblyHubClient(NavigationManager navigation) {
        _navigation = navigation;
    }

    public async Task<AssemblyConsoleJoinDTO> JoinConsoleAsync(Guid eventId) {
        await EnsureConnectedAsync();
        consoleEventId = eventId;
        return await connection!.InvokeAsync<AssemblyConsoleJoinDTO>("JoinConsole", eventId);
    }

    public async Task<AssemblyOverlayDTO> JoinOverlayAsync(Guid eventId) {
        await EnsureConnectedAsync();
        overlayEventId = eventId;
        return await connection!.InvokeAsync<AssemblyOverlayDTO>("JoinOverlay", eventId);
    }

    public async Task<AssemblyCommandResult> CommandAsync(string method, params object?[] arguments) {
        await EnsureConnectedAsync();
        return await connection!.InvokeCoreAsync<AssemblyCommandResult>(method, arguments);
    }

    public async ValueTask DisposeAsync() {
        if (connection != null) {
            await connection.DisposeAsync();
            connection = null;
        }
        _connectLock.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task EnsureConnectedAsync() {
        if (connection?.State == HubConnectionState.Connected)
            return;

        await _connectLock.WaitAsync();
        try {
            if (connection?.State == HubConnectionState.Connected)
                return;

            connection ??= Build();
            if (connection.State == HubConnectionState.Disconnected)
                await connection.StartAsync();
        } finally {
            _connectLock.Release();
        }
    }

    private HubConnection Build() {
        var built = new HubConnectionBuilder()
            .WithUrl(_navigation.ToAbsoluteUri(AssemblyHubMethods.HubPath))
            .WithAutomaticReconnect(new UnlimitedRetryPolicy())
            .Build();

        built.On<AssemblySnapshotDTO>(AssemblyHubMethods.SnapshotChanged, dto => SnapshotChanged?.Invoke(dto));
        built.On<AssemblyOverlayDTO>(AssemblyHubMethods.OverlayChanged, dto => OverlayChanged?.Invoke(dto));
        built.On(AssemblyHubMethods.SessionEnded, () => SessionEnded?.Invoke());

        built.Reconnecting += _ => {
            ConnectionStateChanged?.Invoke(false);
            return Task.CompletedTask;
        };
        built.Reconnected += async _ => {
            await RejoinAsync(built);
            ConnectionStateChanged?.Invoke(true);
        };
        built.Closed += _ => {
            ConnectionStateChanged?.Invoke(false);
            return Task.CompletedTask;
        };
        return built;
    }

    private async Task RejoinAsync(HubConnection current) {
        if (consoleEventId != null) {
            var join = await current.InvokeAsync<AssemblyConsoleJoinDTO>("JoinConsole", consoleEventId.Value);
            ConsoleRejoined?.Invoke(join);
        }
        if (overlayEventId != null) {
            var overlay = await current.InvokeAsync<AssemblyOverlayDTO>("JoinOverlay", overlayEventId.Value);
            OverlayChanged?.Invoke(overlay);
        }
    }

    private class UnlimitedRetryPolicy : IRetryPolicy {
        public TimeSpan? NextRetryDelay(RetryContext retryContext) {
            return TimeSpan.FromSeconds(Math.Min(10, 1 + retryContext.PreviousRetryCount * 2));
        }
    }
}
