using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Server.Messaging;

/// <summary>
/// Outbound delivery channel (email, Telegram, postal PDF). The body is opaque —
/// format-specific rendering happens upstream — and each implementation owns its
/// own queueing/batching/retry.
/// </summary>
public interface IMessageChannel {
    /// <summary>Stable identifier — e.g. <c>"email"</c>, <c>"telegram"</c>, <c>"pdf"</c>.</summary>
    string Id { get; }

    /// <summary>True when the channel has the config it needs to deliver. Dispatchers skip unconfigured channels rather than failing.</summary>
    bool IsConfigured { get; }

    Task<ChannelDeliveryResult> SendAsync(ChannelMessage message, CancellationToken ct = default);
}
