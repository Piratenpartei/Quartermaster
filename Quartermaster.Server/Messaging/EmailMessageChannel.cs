using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Quartermaster.Data.Notifications;
using Quartermaster.Server.Email;

namespace Quartermaster.Server.Messaging;

/// <summary>
/// SMTP delivery: writes a <see cref="NotificationLog"/> row (Pending) and hands an
/// <see cref="EmailMessage"/> to the channel that <see cref="EmailSendingBackgroundService"/>
/// drains. Body is pre-rendered HTML.
/// </summary>
public class EmailMessageChannel : IMessageChannel {
    public const string ChannelId = "email";

    private readonly NotificationLogRepository _logRepo;
    private readonly Channel<EmailMessage> _emailChannel;

    public EmailMessageChannel(NotificationLogRepository logRepo, Channel<EmailMessage> emailChannel) {
        _logRepo = logRepo;
        _emailChannel = emailChannel;
    }

    public string Id => ChannelId;

    /// <summary>
    /// Always true: SMTP host lives in SystemOptions and may be set/cleared at runtime;
    /// the background service surfaces "no host configured" via the per-message error log.
    /// </summary>
    public bool IsConfigured => true;

    public NotificationBodyFormat BodyFormat => NotificationBodyFormat.Html;

    public Task<ChannelDeliveryResult> SendAsync(ChannelMessage message, CancellationToken ct = default) {
        var meta = message.Metadata;
        var templateIdentifier = TryGet(meta, NotificationLogMetadataKeys.TemplateIdentifier);
        var triggerId = TryGet(meta, NotificationLogMetadataKeys.TriggerId);
        Guid? recipientUserId = null;
        var recipientUserIdStr = TryGet(meta, NotificationLogMetadataKeys.RecipientUserId);
        if (recipientUserIdStr != null && Guid.TryParse(recipientUserIdStr, out var parsed)) {
            recipientUserId = parsed;
        }

        var log = new NotificationLog {
            ChannelId = ChannelId,
            Recipient = message.ChannelAddress,
            RecipientUserId = recipientUserId,
            Subject = message.Subject,
            TriggerId = triggerId,
            TemplateIdentifier = templateIdentifier,
            SourceEntityType = message.SourceEntityType,
            SourceEntityId = message.SourceEntityId,
            Status = "Pending",
            AttemptCount = 0,
            CreatedAt = DateTime.UtcNow,
            Body = message.Body
        };
        _logRepo.Create(log);

        var queued = _emailChannel.Writer.TryWrite(new EmailMessage(log.Id, log.Recipient, log.Subject, log.Body));
        return Task.FromResult(queued
            ? ChannelDeliveryResult.Ok()
            : ChannelDeliveryResult.Fail("Email queue rejected the message."));
    }

    private static string? TryGet(IReadOnlyDictionary<string, string>? meta, string key) {
        return meta != null && meta.TryGetValue(key, out var v) ? v : null;
    }
}
