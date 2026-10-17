using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Google.Protobuf;
using Quartermaster.Server.AssemblyAssistant.Mumble;
using Quartermaster.Server.AssemblyAssistant.Mumble.Proto;

namespace Quartermaster.Server.Tests.AssemblyAssistant.Fakes;

/// <summary>
/// Scripted Mumble server on 127.0.0.1 for connection tests: performs the handshake with a configurable channel and user list,
/// records every frame it receives and can push messages, answer or ignore pings and confirm or deny moves.
/// </summary>
public class FakeMumbleServer : IAsyncDisposable {
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly X509Certificate2 _certificate = CreateCertificate("CN=fake-mumble");
    private readonly CancellationTokenSource _cts = new();
    private readonly Channel<(MumbleMessageType Type, byte[] Payload)> _received = Channel.CreateUnbounded<(MumbleMessageType, byte[])>();
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly List<(MumbleMessageType Type, byte[] Payload)> _receivedLog = [];
    private readonly Lock _lock = new();
    private Task? acceptTask;
    private Stream? current;

    public uint BotSession { get; set; } = 100;
    public uint BotInitialChannel { get; set; }
    public List<ChannelState> Channels { get; } = [];
    public List<UserState> Users { get; } = [];
    public bool AnswerPings { get; set; } = true;
    public bool ConfirmMoves { get; set; } = true;
    public PermissionDenied? DenyMovesWith { get; set; }
    public bool ConfirmRegistration { get; set; } = true;
    public int ConnectionCount { get; private set; }
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
    public string CertificateSha256 => Convert.ToHexString(_certificate.GetCertHash(HashAlgorithmName.SHA256));

    public void Start() {
        _listener.Start();
        acceptTask = Task.Run(AcceptLoopAsync);
    }

    public static X509Certificate2 CreateCertificate(string subject) {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx, "pw"), "pw");
    }

    public static string CreateClientCertificateBase64(string password) {
        using var certificate = CreateCertificate("CN=Versammlungsassistent");
        return Convert.ToBase64String(certificate.Export(X509ContentType.Pfx, password));
    }

    public async Task<T> WaitForAsync<T>(MumbleMessageType type, MessageParser<T> parser, Func<T, bool>? predicate = null, int timeoutMs = 5000)
        where T : IMessage<T> {
        using var timeout = new CancellationTokenSource(timeoutMs);
        while (true) {
            var (receivedType, payload) = await _received.Reader.ReadAsync(timeout.Token);
            if (receivedType != type)
                continue;

            var message = parser.ParseFrom(payload);
            if (predicate == null || predicate(message))
                return message;
        }
    }

    public List<(MumbleMessageType Type, byte[] Payload)> ReceivedLog() {
        using (_lock.EnterScope()) {
            return [.. _receivedLog];
        }
    }

    public async Task SendAsync(MumbleMessageType type, IMessage message) {
        await SendRawAsync(type, message.ToByteArray());
    }

    public async Task SendRawAsync(MumbleMessageType type, byte[] payload) {
        var target = current ?? throw new InvalidOperationException("No client connected.");
        await _writeGate.WaitAsync();
        try {
            await MumbleFrameCodec.WriteRawAsync(target, type, payload, CancellationToken.None);
        } finally {
            _writeGate.Release();
        }
    }

    public async Task SendGarbageAsync() {
        var target = current ?? throw new InvalidOperationException("No client connected.");
        await _writeGate.WaitAsync();
        try {
            await target.WriteAsync(new byte[] { 0x00, 0x01, 0x7F, 0xFF, 0xFF, 0xFF });
            await target.FlushAsync();
        } finally {
            _writeGate.Release();
        }
    }

    public async Task WaitForConnectionCountAsync(int count, int timeoutMs = 5000) {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (ConnectionCount < count) {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Expected {count} connections, saw {ConnectionCount}.");
            await Task.Delay(20);
        }
    }

    public async ValueTask DisposeAsync() {
        await _cts.CancelAsync();
        _listener.Stop();
        if (acceptTask != null) {
            try {
                await acceptTask;
            } catch (OperationCanceledException) {
            } catch (SocketException) {
            }
        }
        _certificate.Dispose();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task AcceptLoopAsync() {
        while (!_cts.IsCancellationRequested) {
            var client = await _listener.AcceptTcpClientAsync(_cts.Token);
            _ = Task.Run(() => ServeAsync(client));
        }
    }

    private async Task ServeAsync(TcpClient client) {
        using (client) {
            await using var ssl = new SslStream(client.GetStream(), false, (_, _, _, _) => true);
            try {
                await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions {
                    ServerCertificate = _certificate,
                    ClientCertificateRequired = true
                }, _cts.Token);
            } catch (Exception ex) when (ex is AuthenticationException || ex is IOException) {
                return;
            }

            current = ssl;
            ConnectionCount++;
            try {
                await HandshakeAsync();
                while (!_cts.IsCancellationRequested) {
                    var (type, payload) = await MumbleFrameCodec.ReadAsync(ssl, _cts.Token);
                    using (_lock.EnterScope()) {
                        _receivedLog.Add((type, payload));
                    }
                    await _received.Writer.WriteAsync((type, payload));
                    await ReactAsync(type, payload);
                }
            } catch (Exception ex) when (ex is IOException || ex is OperationCanceledException || ex is ObjectDisposedException) {
            } finally {
                if (current == ssl)
                    current = null;
            }
        }
    }

    private async Task HandshakeAsync() {
        await SendAsync(MumbleMessageType.Version, new Quartermaster.Server.AssemblyAssistant.Mumble.Proto.Version { Version_ = 0x00010304, Release = "1.3.4" });
        foreach (var channel in Channels)
            await SendAsync(MumbleMessageType.ChannelState, channel);
        foreach (var user in Users)
            await SendAsync(MumbleMessageType.UserState, user);
        await SendAsync(MumbleMessageType.UserState, new UserState { Session = BotSession, Name = "Versammlungsassistent", ChannelId = BotInitialChannel });
        await SendAsync(MumbleMessageType.ServerSync, new ServerSync { Session = BotSession, WelcomeText = "Willkommen" });
    }

    private async Task ReactAsync(MumbleMessageType type, byte[] payload) {
        if (type == MumbleMessageType.Ping && AnswerPings) {
            await SendRawAsync(MumbleMessageType.Ping, payload);
            return;
        }
        if (type != MumbleMessageType.UserState)
            return;

        var state = UserState.Parser.ParseFrom(payload);
        if (state.HasUserId && state.Session == BotSession && ConfirmRegistration) {
            await SendAsync(MumbleMessageType.UserState, new UserState { Session = BotSession, UserId = 4711 });
            return;
        }
        if (!state.HasChannelId)
            return;

        if (DenyMovesWith != null && state.Session != BotSession) {
            await SendAsync(MumbleMessageType.PermissionDenied, DenyMovesWith);
            return;
        }
        if (ConfirmMoves)
            await SendAsync(MumbleMessageType.UserState, new UserState { Session = state.Session, ChannelId = state.ChannelId, Actor = BotSession });
    }
}
