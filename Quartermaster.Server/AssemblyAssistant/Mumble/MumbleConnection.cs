using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Quartermaster.Server.AssemblyAssistant.Mumble.Proto;

namespace Quartermaster.Server.AssemblyAssistant.Mumble;

/// <summary>
/// Mumble control-channel client over TLS. Never sends UDP, so the server tunnels voice through TCP; voice packets are only
/// inspected for the sender. Advertises protocol 1.4.0 so that servers newer than 1.3 keep sending the legacy voice format.
/// </summary>
public class MumbleConnection : IMumbleConnection {
    private const uint AdvertisedVersion = 0x00010400;

    private readonly ILogger<MumbleConnection> _logger;
    private readonly MumbleConnectionTimings _timings;
    private readonly Lock _lock = new();
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly List<PendingMove> _pendingMoves = [];
    private readonly TextMessageQueue _texts;

    private CancellationTokenSource? runCts;
    private Task? runTask;
    private Stream? stream;
    private MumbleServerModel? model;
    private SpeakingTracker? tracker;
    private TaskCompletionSource<bool>? pendingRegistration;
    private uint? homeChannelId;
    private int missedPings;

    public MumbleConnectionState State { get; private set; } = MumbleConnectionState.Disconnected;
    public string? ServerVersion { get; private set; }
    public string? LastServerCertificateSha256 { get; private set; }

    public event Action<MumbleEvent>? EventRaised;

    public MumbleConnection(ILogger<MumbleConnection> logger, MumbleConnectionTimings timings) {
        _logger = logger;
        _timings = timings;
        _texts = new TextMessageQueue(timings.TextInterval);
    }

    public MumbleServerSnapshot Snapshot() {
        using (_lock.EnterScope()) {
            return model?.Snapshot() ?? MumbleServerSnapshot.Empty;
        }
    }

    public Task StartAsync(MumbleSettings settings, uint? homeChannelId, CancellationToken ct) {
        if (runTask != null)
            throw new InvalidOperationException("The Mumble connection is already running.");
        if (!settings.IsConfigured())
            throw new InvalidOperationException("The Mumble connection is not configured.");

        var certificate = X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(settings.CertificateBase64), settings.CertificatePassword);
        this.homeChannelId = homeChannelId;
        runCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        runTask = Task.Run(() => RunAsync(settings, certificate, runCts.Token));
        return Task.CompletedTask;
    }

    public async Task StopAsync() {
        if (runCts == null || runTask == null)
            return;

        await runCts.CancelAsync();
        try {
            await runTask;
        } catch (OperationCanceledException) {
        }

        runCts.Dispose();
        runCts = null;
        runTask = null;
    }

    public async ValueTask DisposeAsync() {
        await StopAsync();
        _writeGate.Dispose();
        GC.SuppressFinalize(this);
    }

    public async Task<MumbleMoveResult> MoveAsync(uint session, uint channelId, CancellationToken ct) {
        PendingMove pending;
        using (_lock.EnterScope()) {
            if (State != MumbleConnectionState.Synced || model == null)
                return new MumbleMoveResult(MumbleMoveOutcome.NotConnected, null);

            var user = model.GetUser(session);
            if (user == null)
                return new MumbleMoveResult(MumbleMoveOutcome.UnknownUser, null);
            // The server silently drops a move into the current channel.
            if (user.ChannelId == channelId)
                return new MumbleMoveResult(MumbleMoveOutcome.Moved, null);

            pending = new PendingMove(session, channelId, new TaskCompletionSource<MumbleMoveResult>(TaskCreationOptions.RunContinuationsAsynchronously));
            _pendingMoves.Add(pending);
        }

        try {
            await SendAsync(MumbleMessageType.UserState, new UserState { Session = session, ChannelId = channelId }, ct);
            var finished = await Task.WhenAny(pending.Result.Task, Task.Delay(_timings.MoveTimeout, ct));
            if (finished == pending.Result.Task)
                return await pending.Result.Task;

            return new MumbleMoveResult(MumbleMoveOutcome.Timeout, null);
        } catch (IOException ex) {
            _logger.LogWarning(ex, "Mumble move of session {Session} could not be sent", session);
            return new MumbleMoveResult(MumbleMoveOutcome.NotConnected, ex.Message);
        } finally {
            using (_lock.EnterScope()) {
                _pendingMoves.Remove(pending);
            }
        }
    }

    public void QueueText(uint session, string message) {
        using (_lock.EnterScope()) {
            _texts.Enqueue(session, message);
        }
    }

    public async Task<bool> RegisterSelfAsync(CancellationToken ct) {
        TaskCompletionSource<bool> registration;
        uint own;
        using (_lock.EnterScope()) {
            if (State != MumbleConnectionState.Synced || model?.OwnSession == null)
                return false;

            own = model.OwnSession.Value;
            if (model.GetUser(own)?.UserId != null)
                return true;

            registration = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            pendingRegistration = registration;
        }

        await SendAsync(MumbleMessageType.UserState, new UserState { Session = own, UserId = 0 }, ct);
        var finished = await Task.WhenAny(registration.Task, Task.Delay(_timings.MoveTimeout, ct));
        return finished == registration.Task && await registration.Task;
    }

    private async Task RunAsync(MumbleSettings settings, X509Certificate2 certificate, CancellationToken ct) {
        var attempt = 0;
        while (!ct.IsCancellationRequested) {
            SetState(attempt == 0 ? MumbleConnectionState.Connecting : MumbleConnectionState.Reconnecting, null);
            try {
                await RunSessionAsync(settings, certificate, ct);
            } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
                break;
            } catch (Exception ex) {
                _logger.LogWarning(ex, "Mumble connection to {Host}:{Port} failed", settings.Host, settings.Port);
                SetState(MumbleConnectionState.Reconnecting, ex.Message);
            }

            AbortPending();
            if (ct.IsCancellationRequested)
                break;

            var wasSynced = model?.IsSynced == true;
            attempt = wasSynced ? 1 : attempt + 1;
            var delay = _timings.ReconnectBackoff[Math.Min(attempt - 1, _timings.ReconnectBackoff.Count - 1)];
            try {
                await Task.Delay(delay, ct);
            } catch (OperationCanceledException) {
                break;
            }
        }

        AbortPending();
        certificate.Dispose();
        SetState(MumbleConnectionState.Disconnected, null);
    }

    private async Task RunSessionAsync(MumbleSettings settings, X509Certificate2 certificate, CancellationToken ct) {
        using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var tcp = new TcpClient();
        using (var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct)) {
            connectCts.CancelAfter(_timings.ConnectTimeout);
            await tcp.ConnectAsync(settings.Host, settings.Port, connectCts.Token);
        }

        await using var ssl = new SslStream(tcp.GetStream(), false);
        await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions {
            TargetHost = settings.Host,
            ClientCertificates = new X509CertificateCollection { certificate },
            RemoteCertificateValidationCallback = (_, serverCertificate, _, errors) => ValidateServerCertificate(settings, serverCertificate, errors)
        }, ct);

        using (_lock.EnterScope()) {
            stream = ssl;
            model = new MumbleServerModel();
            tracker = new SpeakingTracker(settings.VoiceHangover);
            missedPings = 0;
        }

        await SendAsync(MumbleMessageType.Version, new Proto.Version {
            Version_ = AdvertisedVersion,
            Release = "Quartermaster",
            Os = "Linux"
        }, ct);
        await SendAsync(MumbleMessageType.Authenticate, new Authenticate { Username = settings.Username, Opus = true }, ct);

        var pingTask = PingLoopAsync(sessionCts);
        var tickTask = TickLoopAsync(sessionCts.Token);
        try {
            while (true) {
                var (type, payload) = await MumbleFrameCodec.ReadAsync(ssl, sessionCts.Token);
                await HandleAsync(type, payload, sessionCts.Token);
            }
        } catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
            throw new MumbleProtocolException("The Mumble server stopped answering pings.");
        } finally {
            await sessionCts.CancelAsync();
            await IgnoreCancellation(pingTask);
            await IgnoreCancellation(tickTask);
            using (_lock.EnterScope()) {
                stream = null;
            }
        }
    }

    private bool ValidateServerCertificate(MumbleSettings settings, X509Certificate? serverCertificate, SslPolicyErrors errors) {
        if (serverCertificate == null)
            return false;

        var fingerprint = Convert.ToHexString(serverCertificate.GetCertHash(HashAlgorithmName.SHA256));
        LastServerCertificateSha256 = fingerprint;
        if (settings.ServerCertificateSha256 == null)
            return errors == SslPolicyErrors.None;

        var pinned = settings.ServerCertificateSha256.Replace(":", "").Replace(" ", "");
        return string.Equals(pinned, fingerprint, StringComparison.OrdinalIgnoreCase);
    }

    private async Task HandleAsync(MumbleMessageType type, byte[] payload, CancellationToken ct) {
        var events = new List<MumbleEvent>();
        var followUps = new List<UserState>();

        try {
            using (_lock.EnterScope()) {
                HandleLocked(type, payload, events, followUps);
            }
        } catch (InvalidProtocolBufferException ex) {
            throw new MumbleProtocolException($"Malformed Mumble message of type {type}: {ex.Message}");
        }

        foreach (var message in followUps)
            await SendAsync(MumbleMessageType.UserState, message, ct);
        Raise(events);
    }

    private void HandleLocked(MumbleMessageType type, byte[] payload, List<MumbleEvent> events, List<UserState> followUps) {
        switch (type) {
            case MumbleMessageType.Version:
                ServerVersion = FormatVersion(Proto.Version.Parser.ParseFrom(payload).Version_);
                break;
            case MumbleMessageType.Reject:
                var reject = Reject.Parser.ParseFrom(payload);
                throw new MumbleProtocolException($"The Mumble server rejected the connection ({reject.Type}): {reject.Reason}");
            case MumbleMessageType.Ping:
                missedPings = 0;
                break;
            case MumbleMessageType.ServerSync:
                events.AddRange(model!.ApplyServerSync(ServerSync.Parser.ParseFrom(payload)));
                State = MumbleConnectionState.Synced;
                events.Add(new MumbleConnectionChanged(MumbleConnectionState.Synced, null));
                followUps.Add(InitialSelfState());
                break;
            case MumbleMessageType.ChannelState:
                events.AddRange(model!.Apply(ChannelState.Parser.ParseFrom(payload)));
                break;
            case MumbleMessageType.ChannelRemove:
                events.AddRange(model!.Apply(ChannelRemove.Parser.ParseFrom(payload)));
                break;
            case MumbleMessageType.UserState:
                HandleUserState(UserState.Parser.ParseFrom(payload), events, followUps);
                break;
            case MumbleMessageType.UserRemove:
                HandleUserRemove(UserRemove.Parser.ParseFrom(payload), events);
                break;
            case MumbleMessageType.PermissionDenied:
                HandlePermissionDenied(PermissionDenied.Parser.ParseFrom(payload), events);
                break;
            case MumbleMessageType.UdpTunnel:
                if (MumbleVoicePacket.TryParse(payload, out var packet)) {
                    var speaking = tracker!.OnPacket(packet, DateTime.UtcNow);
                    if (speaking != null)
                        events.Add(speaking);
                }
                break;
        }
    }

    private UserState InitialSelfState() {
        var own = model!.OwnSession!.Value;
        var self = new UserState { Session = own, SelfMute = true, SelfDeaf = false };
        if (homeChannelId != null && model.GetUser(own)?.ChannelId != homeChannelId)
            self.ChannelId = homeChannelId.Value;

        return self;
    }

    private void HandleUserState(UserState message, List<MumbleEvent> events, List<UserState> followUps) {
        events.AddRange(model!.Apply(message));
        if (!message.HasSession)
            return;

        var own = model.OwnSession;
        if (own != null && message.Session == own.Value) {
            HandleOwnUserState(message, events, followUps);
            return;
        }

        if (!message.HasChannelId)
            return;

        var pending = _pendingMoves.FirstOrDefault(p => p.Session == message.Session && p.ChannelId == message.ChannelId);
        pending?.Result.TrySetResult(new MumbleMoveResult(MumbleMoveOutcome.Moved, null));
    }

    private void HandleOwnUserState(UserState message, List<MumbleEvent> events, List<UserState> followUps) {
        if (message.HasUserId)
            pendingRegistration?.TrySetResult(true);

        if (homeChannelId == null || !model!.IsSynced || !message.HasChannelId || message.ChannelId == homeChannelId)
            return;

        followUps.Add(new UserState { Session = message.Session, ChannelId = homeChannelId.Value });
        events.Add(new MumbleSelfMovedBack(message.ChannelId));
    }

    private void HandleUserRemove(UserRemove message, List<MumbleEvent> events) {
        events.AddRange(model!.Apply(message));
        var stopped = tracker!.Forget(message.Session, DateTime.UtcNow);
        if (stopped != null)
            events.Add(stopped);

        foreach (var pending in _pendingMoves.Where(p => p.Session == message.Session))
            pending.Result.TrySetResult(new MumbleMoveResult(MumbleMoveOutcome.UnknownUser, null));
    }

    // A denied Move names the bot (the actor) as session, a denied Enter names the moved user, ChannelFull names nobody;
    // so the denial goes to the matching pending move, otherwise to the oldest one.
    private void HandlePermissionDenied(PermissionDenied message, List<MumbleEvent> events) {
        var session = message.HasSession ? message.Session : (uint?)null;
        var channelId = message.HasChannelId ? message.ChannelId : (uint?)null;
        var reason = string.IsNullOrEmpty(message.Reason) ? message.Type.ToString() : message.Reason;
        events.Add(new MumblePermissionDenied(session, channelId, reason));

        var pending = _pendingMoves.FirstOrDefault(p => session != null && p.Session == session.Value)
            ?? _pendingMoves.FirstOrDefault();
        if (pending == null)
            return;

        var outcome = message.Type == PermissionDenied.Types.DenyType.ChannelFull ? MumbleMoveOutcome.ChannelFull : MumbleMoveOutcome.PermissionDenied;
        pending.Result.TrySetResult(new MumbleMoveResult(outcome, reason));
    }

    private async Task PingLoopAsync(CancellationTokenSource sessionCts) {
        var ct = sessionCts.Token;
        using var timer = new PeriodicTimer(_timings.PingInterval);
        while (await timer.WaitForNextTickAsync(ct)) {
            int missed;
            using (_lock.EnterScope()) {
                missed = missedPings;
                missedPings++;
            }

            if (missed >= _timings.MaxMissedPings) {
                _logger.LogWarning("Mumble server did not answer {Count} pings, reconnecting", missed);
                await sessionCts.CancelAsync();
                return;
            }

            await SendAsync(MumbleMessageType.Ping, new Ping { Timestamp = (ulong)DateTime.UtcNow.Ticks }, ct);
        }
    }

    private async Task TickLoopAsync(CancellationToken ct) {
        using var timer = new PeriodicTimer(_timings.TickInterval);
        while (await timer.WaitForNextTickAsync(ct)) {
            var now = DateTime.UtcNow;
            List<MumbleEvent> events;
            (uint Session, string Message)? text;
            using (_lock.EnterScope()) {
                events = tracker?.OnTick(now).ToList() ?? [];
                text = State == MumbleConnectionState.Synced ? _texts.DequeueDue(now) : null;
            }

            if (text != null) {
                var message = new TextMessage { Message = text.Value.Message };
                message.Session.Add(text.Value.Session);
                await SendAsync(MumbleMessageType.TextMessage, message, ct);
            }
            Raise(events);
        }
    }

    private async Task SendAsync(MumbleMessageType type, IMessage message, CancellationToken ct) {
        Stream? target;
        using (_lock.EnterScope()) {
            target = stream;
        }
        if (target == null)
            throw new IOException("The Mumble connection is not open.");

        await _writeGate.WaitAsync(ct);
        try {
            await MumbleFrameCodec.WriteAsync(target, type, message, ct);
        } finally {
            _writeGate.Release();
        }
    }

    private void SetState(MumbleConnectionState state, string? error) {
        using (_lock.EnterScope()) {
            if (State == state && error == null)
                return;

            State = state;
        }

        Raise([new MumbleConnectionChanged(state, error)]);
    }

    private void AbortPending() {
        using (_lock.EnterScope()) {
            foreach (var pending in _pendingMoves)
                pending.Result.TrySetResult(new MumbleMoveResult(MumbleMoveOutcome.NotConnected, null));
            pendingRegistration?.TrySetResult(false);
            tracker?.Clear();
        }
    }

    private void Raise(IReadOnlyList<MumbleEvent> events) {
        foreach (var mumbleEvent in events) {
            try {
                EventRaised?.Invoke(mumbleEvent);
            } catch (Exception ex) {
                _logger.LogError(ex, "Handler for Mumble event {Event} failed", mumbleEvent.GetType().Name);
            }
        }
    }

    private static async Task IgnoreCancellation(Task task) {
        try {
            await task;
        } catch (OperationCanceledException) {
        } catch (IOException) {
        }
    }

    private static string FormatVersion(uint version) {
        return $"{version >> 16}.{(version >> 8) & 0xFF}.{version & 0xFF}";
    }

    private record PendingMove(uint Session, uint ChannelId, TaskCompletionSource<MumbleMoveResult> Result);
}
