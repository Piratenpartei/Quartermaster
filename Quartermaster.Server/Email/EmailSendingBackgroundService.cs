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
using Quartermaster.Data.Email;
using Quartermaster.Data.Options;

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

    /// <summary>
    /// On startup, re-enqueue any EmailLog entries still marked Pending.
    /// These are messages that were queued in memory when the server last shut down
    /// (crash or restart) and never got sent. The body is restored from the DB.
    /// </summary>
    private void RequeuePendingLogs() {
        using var scope = _services.CreateScope();
        var emailLogRepo = scope.ServiceProvider.GetRequiredService<EmailLogRepository>();
        var pending = emailLogRepo.GetPending();

        if (pending.Count == 0)
            return;

        _logger.LogInformation("Re-enqueuing {Count} pending email(s) after startup", pending.Count);
        foreach (var log in pending) {
            _channel.Writer.TryWrite(new EmailMessage(log.Id, log.Recipient, log.Subject, log.HtmlBody ?? ""));
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
        var emailLogRepo = scope.ServiceProvider.GetRequiredService<EmailLogRepository>();

        var config = ReadSmtpConfig(optionRepo);
        if (config == null) {
            foreach (var msg in batch) {
                emailLogRepo.IncrementAttempt(msg.EmailLogId);
                emailLogRepo.UpdateStatus(msg.EmailLogId, "Failed", "SMTP nicht konfiguriert.", null);
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
                    emailLogRepo.IncrementAttempt(msg.EmailLogId);
                    await SendOneAsync(client, msg, config, ct);
                    emailLogRepo.UpdateStatus(msg.EmailLogId, "Sent", null, DateTime.UtcNow);
                    _logger.LogInformation("Email sent to {Recipient}", msg.To);
                } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
                    throw;
                } catch (ServiceNotConnectedException) {
                    // Connection dropped mid-batch — re-queue this message and the rest
                    _logger.LogWarning("SMTP connection dropped, re-queueing remaining {Count} messages",
                        batch.Count - i);
                    for (int j = i; j < batch.Count; j++)
                        await HandleFailure(batch[j], "SMTP-Verbindung abgebrochen.", ct);
                    return;
                } catch (Exception ex) {
                    _logger.LogError(ex, "Failed to send email to {Recipient}", msg.To);
                    await HandleFailure(msg, ex.Message, ct);
                }
            }
        } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
            // Shutdown — pending messages remain in DB with status Pending and get re-queued on next start
        } catch (Exception ex) {
            _logger.LogError(ex, "SMTP connection error, re-queueing {Count} messages", batch.Count);
            foreach (var msg in batch)
                await HandleFailure(msg, ex.Message, ct);
        } finally {
            if (connected) {
                try {
                    await client.DisconnectAsync(true, ct);
                } catch {
                    // best-effort disconnect
                }
            }
        }
    }

    private async Task SendOneAsync(SmtpClient client, EmailMessage message, SmtpConfig config, CancellationToken ct) {
        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(config.SenderName, config.SenderAddress));
        mimeMessage.To.Add(MailboxAddress.Parse(message.To));
        mimeMessage.Subject = message.Subject;
        mimeMessage.Body = new TextPart("html") { Text = message.HtmlBody };

        await client.SendAsync(mimeMessage, ct);
    }

    private static SmtpConfig? ReadSmtpConfig(OptionRepository optionRepo) {
        var host = optionRepo.GetGlobalValue("email.smtp.host")?.Value;
        var senderAddress = optionRepo.GetGlobalValue("email.smtp.sender_address")?.Value;
        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(senderAddress))
            return null;

        var portStr = optionRepo.GetGlobalValue("email.smtp.port")?.Value ?? "587";
        if (!int.TryParse(portStr, out var port))
            port = 587;

        return new SmtpConfig(
            host,
            port,
            optionRepo.GetGlobalValue("email.smtp.username")?.Value,
            optionRepo.GetGlobalValue("email.smtp.password")?.Value,
            senderAddress,
            optionRepo.GetGlobalValue("email.smtp.sender_name")?.Value ?? "Quartermaster",
            optionRepo.GetGlobalValue("email.smtp.use_ssl")?.Value?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? true
        );
    }

    private async Task HandleFailure(EmailMessage message, string error, CancellationToken ct) {
        using var scope = _services.CreateScope();
        var emailLogRepo = scope.ServiceProvider.GetRequiredService<EmailLogRepository>();

        var log = emailLogRepo.GetById(message.EmailLogId);

        if (log != null && log.AttemptCount < MaxRetries) {
            _logger.LogWarning("Retry {Attempt}/{Max} for email to {Recipient}",
                log.AttemptCount, MaxRetries, message.To);
            await Task.Delay(TimeSpan.FromSeconds(10 * log.AttemptCount), ct);
            _channel.Writer.TryWrite(message);
        } else {
            emailLogRepo.UpdateStatus(message.EmailLogId, "Failed", error, null);
        }
    }

    private record SmtpConfig(
        string Host,
        int Port,
        string? Username,
        string? Password,
        string SenderAddress,
        string SenderName,
        bool UseSsl
    );
}
