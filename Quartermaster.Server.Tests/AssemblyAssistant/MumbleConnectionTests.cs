using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Server.AssemblyAssistant.Mumble;
using Quartermaster.Server.AssemblyAssistant.Mumble.Proto;
using Quartermaster.Server.Tests.AssemblyAssistant.Fakes;

namespace Quartermaster.Server.Tests.AssemblyAssistant;

[NotInParallel(nameof(MumbleConnectionTests))]
public class MumbleConnectionTests {
    private const uint RootChannel = 0;
    private const uint ChairChannel = 1;
    private const uint SpeakerChannel = 2;
    private const uint ListenerChannel = 3;
    private const string CertificatePassword = "secret";

    private static readonly MumbleConnectionTimings FastTimings = new(
        TimeSpan.FromMilliseconds(100),
        2,
        TimeSpan.FromSeconds(2),
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromMilliseconds(25),
        TimeSpan.FromMilliseconds(200),
        [TimeSpan.FromMilliseconds(50)]);

    private static readonly string ClientCertificate = FakeMumbleServer.CreateClientCertificateBase64(CertificatePassword);

    private FakeMumbleServer _server = default!;
    private MumbleConnection _connection = default!;
    private Channel<MumbleEvent> _events = default!;

    [Before(Test)]
    public void Setup() {
        _server = new FakeMumbleServer { BotInitialChannel = RootChannel };
        _server.Channels.Add(new ChannelState { ChannelId = RootChannel, Name = "Root" });
        _server.Channels.Add(new ChannelState { ChannelId = ChairChannel, Parent = RootChannel, Name = "Versammlungsleitung" });
        _server.Channels.Add(new ChannelState { ChannelId = SpeakerChannel, Parent = RootChannel, Name = "Redner" });
        _server.Channels.Add(new ChannelState { ChannelId = ListenerChannel, Parent = RootChannel, Name = "Zuhörer" });
        _server.Users.Add(new UserState { Session = 5, Name = "Pirat", ChannelId = ListenerChannel, Hash = "abc" });
        _server.Start();

        _connection = new MumbleConnection(NullLogger<MumbleConnection>.Instance, FastTimings);
        _events = Channel.CreateUnbounded<MumbleEvent>();
        _connection.EventRaised += e => _events.Writer.TryWrite(e);
    }

    [After(Test)]
    public async Task Teardown() {
        await _connection.DisposeAsync();
        await _server.DisposeAsync();
    }

    private MumbleSettings Settings(string? pin = null) {
        return new MumbleSettings("127.0.0.1", _server.Port, "Versammlungsassistent", ClientCertificate, CertificatePassword,
            pin ?? _server.CertificateSha256, TimeSpan.FromMilliseconds(200), new Dictionary<ChannelRole, uint>());
    }

    private async Task<T> WaitForEventAsync<T>(Func<T, bool>? predicate = null, int timeoutMs = 5000) where T : MumbleEvent {
        using var timeout = new CancellationTokenSource(timeoutMs);
        while (true) {
            var next = await _events.Reader.ReadAsync(timeout.Token);
            if (next is T typed && (predicate == null || predicate(typed)))
                return typed;
        }
    }

    private async Task<MumbleSynced> StartAndSyncAsync() {
        await _connection.StartAsync(Settings(), ChairChannel, CancellationToken.None);
        return await WaitForEventAsync<MumbleSynced>();
    }

    private static byte[] OpusPacket(uint session, bool terminator) {
        var size = terminator ? 0x2003 : 0x03;
        if (size < 0x80)
            return [0x80, (byte)session, 0x07, (byte)size, 0xAA, 0xBB, 0xCC];

        return [0x80, (byte)session, 0x07, (byte)(0x80 | (size >> 8)), (byte)(size & 0xFF), 0xAA, 0xBB, 0xCC];
    }

    [Test]
    public async Task Handshake_sends_version_and_authenticate() {
        await _connection.StartAsync(Settings(), ChairChannel, CancellationToken.None);

        var version = await _server.WaitForAsync(MumbleMessageType.Version, Quartermaster.Server.AssemblyAssistant.Mumble.Proto.Version.Parser);
        var authenticate = await _server.WaitForAsync(MumbleMessageType.Authenticate, Authenticate.Parser);

        await Assert.That(version.Version_).IsEqualTo(0x00010400u);
        await Assert.That(authenticate.Username).IsEqualTo("Versammlungsassistent");
        await Assert.That(authenticate.Opus).IsTrue();
    }

    [Test]
    public async Task Sync_raises_snapshot_with_users() {
        var synced = await StartAndSyncAsync();

        await Assert.That(synced.Snapshot.OwnSession).IsEqualTo(_server.BotSession);
        await Assert.That(synced.Snapshot.Users.Any(u => u.Name == "Pirat" && u.ChannelId == ListenerChannel)).IsTrue();
        await Assert.That(synced.Snapshot.Channels.Count).IsEqualTo(4);
        await Assert.That(_connection.State).IsEqualTo(MumbleConnectionState.Synced);
        await Assert.That(_connection.ServerVersion).IsEqualTo("1.3.4");
    }

    [Test]
    public async Task Bot_moves_itself_to_home_channel() {
        await StartAndSyncAsync();

        var self = await _server.WaitForAsync(MumbleMessageType.UserState, UserState.Parser, s => s.Session == _server.BotSession);

        await Assert.That(self.ChannelId).IsEqualTo(ChairChannel);
        await Assert.That(self.SelfMute).IsTrue();
        await Assert.That(self.SelfDeaf).IsFalse();
    }

    [Test]
    public async Task Bot_moves_back_when_moved_away() {
        await StartAndSyncAsync();
        await _server.WaitForAsync(MumbleMessageType.UserState, UserState.Parser, s => s.Session == _server.BotSession && s.HasChannelId);

        await _server.SendAsync(MumbleMessageType.UserState, new UserState { Session = _server.BotSession, ChannelId = ListenerChannel });

        var movedBack = await WaitForEventAsync<MumbleSelfMovedBack>();
        var request = await _server.WaitForAsync(MumbleMessageType.UserState, UserState.Parser, s => s.Session == _server.BotSession && s.HasChannelId);
        await Assert.That(movedBack.FromChannelId).IsEqualTo(ListenerChannel);
        await Assert.That(request.ChannelId).IsEqualTo(ChairChannel);
    }

    [Test]
    public async Task Move_confirmed_by_userstate() {
        await StartAndSyncAsync();

        var result = await _connection.MoveAsync(5, SpeakerChannel, CancellationToken.None);

        await Assert.That(result.Outcome).IsEqualTo(MumbleMoveOutcome.Moved);
        await Assert.That(_connection.Snapshot().Users.Single(u => u.Session == 5).ChannelId).IsEqualTo(SpeakerChannel);
        var entered = await WaitForEventAsync<MumbleUserEntered>(e => e.User.Session == 5);
        await Assert.That(entered.PreviousChannelId).IsEqualTo(ListenerChannel);
    }

    [Test]
    public async Task Move_permission_denied_is_reported() {
        _server.DenyMovesWith = new PermissionDenied {
            Session = _server.BotSession,
            ChannelId = ListenerChannel,
            Type = PermissionDenied.Types.DenyType.Permission,
            Permission = 0x20
        };
        await StartAndSyncAsync();

        var result = await _connection.MoveAsync(5, SpeakerChannel, CancellationToken.None);

        await Assert.That(result.Outcome).IsEqualTo(MumbleMoveOutcome.PermissionDenied);
        await WaitForEventAsync<MumblePermissionDenied>();
    }

    [Test]
    public async Task Move_without_answer_times_out() {
        _server.ConfirmMoves = false;
        await StartAndSyncAsync();

        var result = await _connection.MoveAsync(5, SpeakerChannel, CancellationToken.None);

        await Assert.That(result.Outcome).IsEqualTo(MumbleMoveOutcome.Timeout);
    }

    [Test]
    public async Task Move_of_unknown_user_is_reported() {
        await StartAndSyncAsync();

        var result = await _connection.MoveAsync(999, SpeakerChannel, CancellationToken.None);

        await Assert.That(result.Outcome).IsEqualTo(MumbleMoveOutcome.UnknownUser);
    }

    [Test]
    public async Task Voice_packet_raises_speaking_started_and_stopped() {
        await StartAndSyncAsync();

        await _server.SendRawAsync(MumbleMessageType.UdpTunnel, OpusPacket(5, terminator: false));
        var started = await WaitForEventAsync<MumbleSpeakingStarted>();
        await _server.SendRawAsync(MumbleMessageType.UdpTunnel, OpusPacket(5, terminator: true));
        var stopped = await WaitForEventAsync<MumbleSpeakingStopped>();

        await Assert.That(started.Session).IsEqualTo(5u);
        await Assert.That(stopped.Session).IsEqualTo(5u);
    }

    [Test]
    public async Task Silence_after_voice_raises_speaking_stopped() {
        await StartAndSyncAsync();

        await _server.SendRawAsync(MumbleMessageType.UdpTunnel, OpusPacket(5, terminator: false));
        await WaitForEventAsync<MumbleSpeakingStarted>();

        var stopped = await WaitForEventAsync<MumbleSpeakingStopped>(timeoutMs: 2000);
        await Assert.That(stopped.Session).IsEqualTo(5u);
    }

    [Test]
    public async Task Text_messages_are_rate_limited() {
        await StartAndSyncAsync();

        _connection.QueueText(5, "eins");
        _connection.QueueText(5, "zwei");
        var first = await _server.WaitForAsync(MumbleMessageType.TextMessage, TextMessage.Parser);
        var firstAt = DateTime.UtcNow;
        var second = await _server.WaitForAsync(MumbleMessageType.TextMessage, TextMessage.Parser);
        var gap = DateTime.UtcNow - firstAt;

        await Assert.That(first.Message).IsEqualTo("eins");
        await Assert.That(first.Session).IsEquivalentTo([5u]);
        await Assert.That(second.Message).IsEqualTo("zwei");
        await Assert.That(gap.TotalMilliseconds).IsGreaterThanOrEqualTo(150);
    }

    [Test]
    public async Task Register_self_waits_for_user_id() {
        await StartAndSyncAsync();

        var registered = await _connection.RegisterSelfAsync(CancellationToken.None);

        await Assert.That(registered).IsTrue();
        await Assert.That(_connection.Snapshot().Users.Single(u => u.Session == _server.BotSession).UserId).IsEqualTo(4711);
    }

    [Test]
    public async Task Missing_pongs_trigger_reconnect() {
        _server.AnswerPings = false;
        await StartAndSyncAsync();

        await _server.WaitForConnectionCountAsync(2);
        await WaitForEventAsync<MumbleSynced>();
    }

    [Test]
    public async Task Garbage_frame_triggers_reconnect() {
        await StartAndSyncAsync();

        await _server.SendGarbageAsync();

        await WaitForEventAsync<MumbleConnectionChanged>(e => e.State == MumbleConnectionState.Reconnecting);
        await WaitForEventAsync<MumbleSynced>();
        await Assert.That(_server.ConnectionCount).IsGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task Wrong_pin_is_rejected() {
        await _connection.StartAsync(Settings(pin: new string('0', 64)), ChairChannel, CancellationToken.None);

        var failure = await WaitForEventAsync<MumbleConnectionChanged>(e => e.State == MumbleConnectionState.Reconnecting && e.Error != null);

        await Assert.That(failure.Error).IsNotNull();
        await Assert.That(_connection.LastServerCertificateSha256).IsEqualTo(_server.CertificateSha256);
    }

    [Test]
    public async Task Stop_disconnects_and_stays_down() {
        await StartAndSyncAsync();

        await _connection.StopAsync();
        var count = _server.ConnectionCount;
        await Task.Delay(300);

        await Assert.That(_connection.State).IsEqualTo(MumbleConnectionState.Disconnected);
        await Assert.That(_server.ConnectionCount).IsEqualTo(count);
        var move = await _connection.MoveAsync(5, SpeakerChannel, CancellationToken.None);
        await Assert.That(move.Outcome).IsEqualTo(MumbleMoveOutcome.NotConnected);
    }
}
