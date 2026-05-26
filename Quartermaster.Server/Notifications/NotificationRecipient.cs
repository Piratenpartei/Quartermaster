using System;

namespace Quartermaster.Server.Notifications;

/// <summary>
/// One resolved recipient of a notification. <see cref="ChannelAddress"/> is the
/// channel-specific destination (email address, Telegram chat id, postal address);
/// <see cref="UserId"/> is the backing user when known. For Phase 1, only the email
/// channel is targeted and <c>ChannelAddress</c> = email.
/// </summary>
public record NotificationRecipient(Guid? UserId, string ChannelAddress);
