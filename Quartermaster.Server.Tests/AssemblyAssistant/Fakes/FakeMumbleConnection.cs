using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Server.AssemblyAssistant.Mumble;

namespace Quartermaster.Server.Tests.AssemblyAssistant.Fakes;

/// <summary>Stands in for the Mumble server in host and hub tests: records moves and texts, events are raised by the test.</summary>
public class FakeMumbleConnection : IMumbleConnection {
    private readonly Lock _lock = new();
    private readonly List<(uint Session, uint ChannelId)> _moves = [];
    private readonly List<(uint Session, string Text)> _texts = [];

    public MumbleConnectionState State { get; set; } = MumbleConnectionState.Disconnected;
    public string? ServerVersion { get; set; } = "1.3.4";
    public string? LastServerCertificateSha256 { get; set; }
    public MumbleServerSnapshot CurrentSnapshot { get; set; } = MumbleServerSnapshot.Empty;
    public Func<uint, uint, MumbleMoveResult> MoveResult { get; set; } = (_, _) => new MumbleMoveResult(MumbleMoveOutcome.Moved, null);

    /// <summary>Called synchronously inside <see cref="MoveAsync"/>, e.g. to raise the resulting UserState event.</summary>
    public Action<uint, uint>? OnMove { get; set; }

    public uint? HomeChannelId { get; private set; }
    public bool Started { get; private set; }
    public bool Stopped { get; private set; }

    public event Action<MumbleEvent>? EventRaised;

    public IReadOnlyList<(uint Session, uint ChannelId)> Moves {
        get {
            using (_lock.EnterScope()) {
                return [.. _moves];
            }
        }
    }

    public IReadOnlyList<(uint Session, string Text)> Texts {
        get {
            using (_lock.EnterScope()) {
                return [.. _texts];
            }
        }
    }

    public void Raise(MumbleEvent mumbleEvent) {
        EventRaised?.Invoke(mumbleEvent);
    }

    public MumbleServerSnapshot Snapshot() {
        return CurrentSnapshot;
    }

    public Task StartAsync(MumbleSettings settings, uint? homeChannelId, CancellationToken ct) {
        HomeChannelId = homeChannelId;
        Started = true;
        return Task.CompletedTask;
    }

    public Task StopAsync() {
        Stopped = true;
        return Task.CompletedTask;
    }

    public Task<MumbleMoveResult> MoveAsync(uint session, uint channelId, CancellationToken ct) {
        using (_lock.EnterScope()) {
            _moves.Add((session, channelId));
        }

        OnMove?.Invoke(session, channelId);
        return Task.FromResult(MoveResult(session, channelId));
    }

    public void QueueText(uint session, string message) {
        using (_lock.EnterScope()) {
            _texts.Add((session, message));
        }
    }

    public Task<bool> RegisterSelfAsync(CancellationToken ct) {
        return Task.FromResult(true);
    }

    public ValueTask DisposeAsync() {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
