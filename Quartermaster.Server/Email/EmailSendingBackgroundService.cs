using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MimeKit;
using Quartermaster.Data.Notifications;
using Quartermaster.Data.Options;
using Quartermaster.Server.Messaging;

namespace Quartermaster.Server.Email;

public class EmailSendingBackgroundService : BackgroundService {
    private readonly Channel<EmailMessage> _channel;
    private readonly IServiceProvider _services;
    private readonly ILogger<EmailSendingBackgroundService> _logger;
    private const int MaxRetries = 3;
    private const int DefaultBatchSize = 50;

    public EmailSendingBackgroundService(
        Channel<EmailMessage> channel,
        IServiceProvider services,
        ILogger<EmailSendingBackgroundService> logger) {
        _channel = channel;
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        RequeuePendingLogs();

        while (!stoppingToken.IsCancellationRequested) {
            try {
                // Block until at least one message is available
                var first = await _channel.Reader.ReadAsync(stoppingToken);
                var batch = new List<EmailMessage> { first };

                // Drain additional immediately-available messages up to batch size
                var batchSize = GetBatchSize();
                while (batch.Count < batchSize && _channel.Reader.TryRead(out var next))
                    batch.Add(next);

                await ProcessBatchAsync(batch, stoppingToken);
            } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                break;
            }
        }
    }

    /// <summary>Re-enqueues SMTP-channel NotificationLog rows that were Pending at last shutdown.</summary>
    private void RequeuePendingLogs() {
        using var scope = _services.CreateScope();
        var logRepo = scope.ServiceProvider.GetRequiredService<NotificationLogRepository>();
        var pending = logRepo.GetPendingForChannel(EmailMessageChannel.ChannelId);

        if (pending.Count == 0)
            return;

        _logger.LogInformation("Re-enqueuing {Count} pending email(s) after startup", pending.Count);
        foreach (var log in pending) {
            _channel.Writer.TryWrite(new EmailMessage(log.Id, log.Recipient, log.Subject, log.Body ?? ""));
        }
    }

    private int GetBatchSize() {
        using var scope = _services.CreateScope();
        var optionRepo = scope.ServiceProvider.GetRequiredService<OptionRepository>();
        var value = optionRepo.GetGlobalValue("email.smtp.batch_size")?.Value;
        if (int.TryParse(value, out var parsed) && parsed > 0)
            return parsed;
        return DefaultBatchSize;
    }

    private async Task ProcessBatchAsync(List<EmailMessage> batch, CancellationToken ct) {
        using var scope = _services.CreateScope();
        var optionRepo = scope.ServiceProvider.GetRequiredService<OptionRepository>();
        var logRepo = scope.ServiceProvider.GetRequiredService<NotificationLogRepository>();

        var config = SmtpConfig.ReadFrom(optionRepo);
        if (config == null) {
            foreach (var msg in batch) {
                logRepo.IncrementAttempt(msg.NotificationLogId);
                logRepo.UpdateStatus(msg.NotificationLogId, "Failed", "SMTP nicht konfiguriert.", null);
                _logger.LogWarning("SMTP not configured, cannot send email to {Recipient}", msg.To);
            }
            return;
        }

        using var client = new SmtpClient();
        var connected = false;

        try {
            await client.ConnectAsync(config.Host, config.Port,
                config.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None, ct);

            if (!string.IsNullOrEmpty(config.Username) && !string.IsNullOrEmpty(config.Password))
                await client.AuthenticateAsync(config.Username, config.Password, ct);

            connected = true;

            for (int i = 0; i < batch.Count; i++) {
                var msg = batch[i];
                try {
                    logRepo.IncrementAttempt(msg.NotificationLogId);
                    await SendOneAsync(client, msg, config, ct);
                    logRepo.UpdateStatus(msg.NotificationLogId, "Sent", null, DateTime.UtcNow);
                    _logger.LogInformation("Email sent to {Recipient}", msg.To);
                } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
                    throw;
                } catch (ServiceNotConnectedException) {
                    _logger.LogWarning("SMTP connection dropped, re-queueing remaining {Count} messages",
                        batch.Count - i);
                    for (int j = i; j < batch.Count; j++)
                        await HandleFailure(batch[j], "SMTP-Verbindung abgebrochen.", ct);
                    return;
                } catch (Exception ex) {
                    _logger.LogError(ex, "Failed to send email to {Recipient}", msg.To);
                    await HandleFailure(msg, $"{ex}", ct);
                }
            }
        } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
            // Shutdown — pending messages remain in DB with status Pending and get re-queued on next start
        } catch (Exception ex) {
            _logger.LogError(ex, "SMTP connection error, re-queueing {Count} messages", batch.Count);
            foreach (var msg in batch)
                await HandleFailure(msg, $"{ex}", ct);
        } finally {
            if (connected) {
                try {
                    await client.DisconnectAsync(true, ct);
                } catch (Exception ex) {
                    _logger.LogWarning(ex, "SMTP disconnect failed (best-effort)");
                }
            }
        }
    }

    private async Task SendOneAsync(SmtpClient client, EmailMessage message, SmtpConfig config, CancellationToken ct) {
        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(config.SenderName, config.SenderAddress));
        mimeMessage.To.Add(MailboxAddress.Parse(message.To));
        mimeMessage.Subject = message.Subject;
        mimeMessage.Body = new TextPart("html") { Text = message.Body };

        await client.SendAsync(mimeMessage, ct);
    }

    private Task HandleFailure(EmailMessage message, string error, CancellationToken ct) {
        using var scope = _services.CreateScope();
        var logRepo = scope.ServiceProvider.GetRequiredService<NotificationLogRepository>();

        var log = logRepo.Get(message.NotificationLogId);

        if (log != null && log.AttemptCount < MaxRetries) {
            _logger.LogWarning("Retry {Attempt}/{Max} for email to {Recipient}",
                log.AttemptCount, MaxRetries, message.To);
            ScheduleRetry(message, TimeSpan.FromSeconds(10 * log.AttemptCount), ct);
        } else {
            logRepo.UpdateStatus(message.NotificationLogId, "Failed", error, null);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Fire-and-forget delayed re-enqueue. Off the consumer loop so the next batch can
    /// start sending immediately. On shutdown the delay observes <paramref name="ct"/>
    /// and the message stays Pending in the DB — <see cref="RequeuePendingLogs"/> picks
    /// it up on next start.
    /// </summary>
    private void ScheduleRetry(EmailMessage message, TimeSpan delay, CancellationToken ct) {
        _ = Task.Run(async () => {
            try {
                await Task.Delay(delay, ct);
                _channel.Writer.TryWrite(message);
            } catch (OperationCanceledException) {
                // Shutdown — log row stays Pending and gets re-enqueued on next start.
            } catch (Exception ex) {
                _logger.LogWarning(ex, "Scheduled retry for {Recipient} failed to re-enqueue", message.To);
            }
        }, CancellationToken.None);
    }
}
