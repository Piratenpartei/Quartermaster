namespace Quartermaster.Server.Messaging;

/// <summary>
/// Outcome of <see cref="IMessageChannel.SendAsync"/>. <see cref="Accepted"/> means
/// the channel took ownership — for queue-backed channels that's "scheduled", not
/// "delivered"; for sync channels it's the actual I/O outcome.
/// </summary>
public record ChannelDeliveryResult(bool Accepted, string? Error = null) {
    public static ChannelDeliveryResult Ok() => new(true);
    public static ChannelDeliveryResult Fail(string error) => new(false, error);
}
