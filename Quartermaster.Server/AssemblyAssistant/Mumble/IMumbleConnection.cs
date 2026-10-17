using System;
using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Server.AssemblyAssistant.Mumble;

public interface IMumbleConnection : IAsyncDisposable {
    MumbleConnectionState State { get; }

    /// <summary>Version of the connected server, e.g. "1.3.4"; null before the first Version message.</summary>
    string? ServerVersion { get; }

    /// <summary>SHA-256 fingerprint of the server certificate seen in the last TLS handshake, for pinning a self-signed certificate.</summary>
    string? LastServerCertificateSha256 { get; }

    /// <summary>Raised on the connection's own threads; handlers must return quickly.</summary>
    event Action<MumbleEvent>? EventRaised;

    MumbleServerSnapshot Snapshot();

    /// <summary>
    /// Returns at once; the connection keeps reconnecting until <see cref="StopAsync"/>. With a home channel the bot moves
    /// there after every sync and back whenever it is moved away; without one it stays where the server puts it.
    /// </summary>
    Task StartAsync(MumbleSettings settings, uint? homeChannelId, CancellationToken ct);

    Task StopAsync();

    Task<MumbleMoveResult> MoveAsync(uint session, uint channelId, CancellationToken ct);

    void QueueText(uint session, string message);

    Task<bool> RegisterSelfAsync(CancellationToken ct);
}
