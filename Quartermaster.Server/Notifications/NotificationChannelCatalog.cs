using System.Collections.Generic;
using Quartermaster.Server.Messaging;

namespace Quartermaster.Server.Notifications;

/// <summary>
/// Display catalog for channels in the preferences UI. <c>Available = false</c> renders
/// the column as a disabled checkbox until the underlying flow is wired (Telegram in
/// Phase 4, PDF in the envelope-rendering feature).
/// </summary>
public record NotificationChannelDescriptor(string ChannelId, string DisplayName, bool Available);

public static class NotificationChannelCatalog {
    public static IReadOnlyList<NotificationChannelDescriptor> All { get; } = new[] {
        new NotificationChannelDescriptor(EmailMessageChannel.ChannelId, "E-Mail", true),
        new NotificationChannelDescriptor(TelegramMessageChannel.ChannelId, "Telegram", true),
        new NotificationChannelDescriptor(PdfMessageChannel.ChannelId, "Brief (Postversand)", false)
    };
}
