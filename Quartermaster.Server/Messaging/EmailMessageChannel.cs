using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Quartermaster.Data.Email;
using Quartermaster.Server.Email;

namespace Quartermaster.Server.Messaging;

/// <summary>
/// SMTP delivery: writes an <see cref="EmailLog"/> row (Pending) and hands an
/// <see cref="EmailMessage"/> to the channel that <see cref="EmailSendingBackgroundService"/>
/// drains. Body is pre-rendered HTML.
/// </summary>
public class EmailMessageChannel : IMessageChannel {
    public const string ChannelId = "smtp";
    public const string TemplateIdentifierMetadataKey = "TemplateIdentifier";

    private readonly EmailLogRepository _emailLogRepo;
    private readonly Channel<EmailMessage> _emailChannel;

    public EmailMessageChannel(EmailLogRepository emailLogRepo, Channel<EmailMessage> emailChannel) {
        _emailLogRepo = emailLogRepo;
        _emailChannel = emailChannel;
    }

    public string Id => ChannelId;

    /// <summary>
    /// Always true: SMTP host lives in SystemOptions and may be set/cleared at runtime;
    /// the background service surfaces "no host configured" via the per-message error log.
    /// </summary>
    public bool IsConfigured => true;

    public Task<ChannelDeliveryResult> SendAsync(ChannelMessage message, CancellationToken ct = default) {
        var templateIdentifier = message.Metadata != null
            && message.Metadata.TryGetValue(TemplateIdentifierMetadataKey, out var v) ? v : null;

        var log = new EmailLog {
            Recipient = message.ChannelAddress,
            Subject = message.Subject,
            TemplateIdentifier = templateIdentifier,
            SourceEntityType = message.SourceEntityType,
            SourceEntityId = message.SourceEntityId,
            Status = "Pending",
            AttemptCount = 0,
            CreatedAt = DateTime.UtcNow,
            HtmlBody = message.Body
        };
        _emailLogRepo.Create(log);

        var queued = _emailChannel.Writer.TryWrite(new EmailMessage(log.Id, log.Recipient, log.Subject, log.HtmlBody));
        return Task.FromResult(queued
            ? ChannelDeliveryResult.Ok()
            : ChannelDeliveryResult.Fail("Email queue rejected the message."));
    }
}
