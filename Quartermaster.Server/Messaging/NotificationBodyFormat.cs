namespace Quartermaster.Server.Messaging;

/// <summary>
/// Indicates the format a channel expects in <see cref="ChannelMessage.Body"/>.
/// The dispatcher renders the channel's body template accordingly.
/// </summary>
public enum NotificationBodyFormat {
    /// <summary>Plain text / channel-native markup (e.g. Telegram-flavored markdown).</summary>
    Text,
    /// <summary>Sanitized HTML — markdown source is converted via <see cref="Rendering.MarkdownService"/>.</summary>
    Html
}
