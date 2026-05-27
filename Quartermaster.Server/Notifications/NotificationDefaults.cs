using Quartermaster.Server.Messaging;

namespace Quartermaster.Server.Notifications;

/// <summary>
/// Default "is this channel on by default" answer when the user hasn't set an explicit
/// override. Email on, others off until the user opts in (Telegram needs link-token
/// discovery, PDF only fits the postal-mail flow).
/// </summary>
public static class NotificationDefaults {
    public static bool IsEnabledByDefault(string channelId) => channelId switch {
        EmailMessageChannel.ChannelId => true,
        _ => false
    };
}
