using System;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
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

        await foreach (var message in _channel.Reader.ReadAllAsync(stoppingToken)) {
            try {
                await SendViaSmtp(message, stoppingToken);
            } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                break;
            } catch (Exception ex) {
                _logger.LogError(ex, "Failed to process email to {Recipient}", message.To);
                await HandleFailure(message, ex.Message, stoppingToken);
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

    private async Task SendViaSmtp(EmailMessage message, CancellationToken ct) {
        using var scope = _services.CreateScope();
        var optionRepo = scope.ServiceProvider.GetRequiredService<OptionRepository>();
        var emailLogRepo = scope.ServiceProvider.GetRequiredService<EmailLogRepository>();

        var host = optionRepo.GetGlobalValue("email.smtp.host")?.Value;
        var portStr = optionRepo.GetGlobalValue("email.smtp.port")?.Value ?? "587";
        var username = optionRepo.GetGlobalValue("email.smtp.username")?.Value;
        var password = optionRepo.GetGlobalValue("email.smtp.password")?.Value;
        var senderAddress = optionRepo.GetGlobalValue("email.smtp.sender_address")?.Value;
        var senderName = optionRepo.GetGlobalValue("email.smtp.sender_name")?.Value ?? "Quartermaster";
        var useSsl = optionRepo.GetGlobalValue("email.smtp.use_ssl")?.Value
            ?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? true;

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(senderAddress)) {
            emailLogRepo.IncrementAttempt(message.EmailLogId);
            emailLogRepo.UpdateStatus(message.EmailLogId, "Failed",
                "SMTP nicht konfiguriert.", null);
            _logger.LogWarning("SMTP not configured, cannot send email to {Recipient}",
                message.To);
            return;
        }

        if (!int.TryParse(portStr, out var port))
            port = 587;

        emailLogRepo.IncrementAttempt(message.EmailLogId);

        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(senderName, senderAddress));
        mimeMessage.To.Add(MailboxAddress.Parse(message.To));
        mimeMessage.Subject = message.Subject;
        mimeMessage.Body = new TextPart("html") { Text = message.HtmlBody };

        using var client = new SmtpClient();
        await client.ConnectAsync(host, port,
            useSsl
                ? MailKit.Security.SecureSocketOptions.StartTls
                : MailKit.Security.SecureSocketOptions.None,
            ct);

        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            await client.AuthenticateAsync(username, password, ct);

        await client.SendAsync(mimeMessage, ct);
        await client.DisconnectAsync(true, ct);

        emailLogRepo.UpdateStatus(message.EmailLogId, "Sent", null, DateTime.UtcNow);
        _logger.LogInformation("Email sent to {Recipient}", message.To);
    }

    private async Task HandleFailure(EmailMessage message, string error,
        CancellationToken ct) {
        using var scope = _services.CreateScope();
        var emailLogRepo = scope.ServiceProvider.GetRequiredService<EmailLogRepository>();

        emailLogRepo.IncrementAttempt(message.EmailLogId);
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
}
