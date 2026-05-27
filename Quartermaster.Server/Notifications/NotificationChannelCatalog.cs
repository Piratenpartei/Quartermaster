using System.Collections.Generic;
using Quartermaster.Server.Messaging;

namespace Quartermaster.Server.Notifications;

/// <summary>
/// Display catalog for channels. <c>Available = false</c> renders the column as a
/// disabled checkbox until the underlying flow is wired. <c>UserSelectable = false</c>
/// hides the channel from the user-preferences UI entirely — for backup channels
/// (e.g. postal mail) that are system-triggered only.
/// </summary>
public record NotificationChannelDescriptor(string ChannelId, string DisplayName, bool Available, bool UserSelectable);

public static class NotificationChannelCatalog {
    public static IReadOnlyList<NotificationChannelDescriptor> All { get; } = new[] {
        new NotificationChannelDescriptor(EmailMessageChannel.ChannelId, "E-Mail", true, true),
        new NotificationChannelDescriptor(TelegramMessageChannel.ChannelId, "Telegram", true, true),
        new NotificationChannelDescriptor(PdfMessageChannel.ChannelId, "Brief (Postversand)", false, false)
    };
}
