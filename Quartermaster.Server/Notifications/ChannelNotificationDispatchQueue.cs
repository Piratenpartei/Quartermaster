using System.Threading.Channels;

namespace Quartermaster.Server.Notifications;

/// <summary>
/// Production queue — writes requests to an unbounded channel that
/// <see cref="NotificationDispatchBackgroundService"/> drains. <see cref="Enqueue"/>
/// returns immediately so submit endpoints don't block on recipient resolution,
/// rendering, or (notably) synchronous Telegram HTTP calls.
/// </summary>
public class ChannelNotificationDispatchQueue : INotificationDispatchQueue {
    private readonly Channel<NotificationDispatchRequest> _channel =
        Channel.CreateUnbounded<NotificationDispatchRequest>();

    public ChannelReader<NotificationDispatchRequest> Reader => _channel.Reader;

    public void Enqueue(NotificationDispatchRequest request) {
        _channel.Writer.TryWrite(request);
    }
}
