namespace Quartermaster.Server.Messaging;

/// <summary>
/// Keys the dispatcher tucks into <see cref="ChannelMessage.Metadata"/> so each
/// channel can hydrate the same <c>NotificationLog</c> columns. Channels ignore
/// keys they don't recognise.
/// </summary>
public static class NotificationLogMetadataKeys {
    public const string TriggerId = "TriggerId";
    public const string TemplateIdentifier = "TemplateIdentifier";
    public const string RecipientUserId = "RecipientUserId";
}
