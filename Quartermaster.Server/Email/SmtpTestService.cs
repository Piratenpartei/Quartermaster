using System;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.Email;

/// <summary>
/// Sends a one-off test email synchronously using the current SMTP settings and reports
/// the outcome, so the setup page can verify configuration without going through the
/// async delivery queue.
/// </summary>
public class SmtpTestService {
    private readonly OptionRepository _optionRepo;

    public SmtpTestService(OptionRepository optionRepo) {
        _optionRepo = optionRepo;
    }

    /// <summary>Returns null on success, or a human-readable error message on failure.</summary>
    public async Task<string?> SendTestAsync(string recipient, CancellationToken ct) {
        var config = SmtpConfig.ReadFrom(_optionRepo);
        if (config == null) {
            return "SMTP ist nicht vollständig konfiguriert (Host und Absenderadresse sind erforderlich).";
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(config.SenderName, config.SenderAddress));
        message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = "Quartermaster: SMTP-Testnachricht";
        message.Body = new TextPart("html") {
            Text = "<p>Dies ist eine Testnachricht von Quartermaster. Wenn du sie erhältst, ist deine SMTP-Konfiguration korrekt.</p>"
        };

        using var client = new SmtpClient();
        try {
            await client.ConnectAsync(config.Host, config.Port,
                config.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None, ct);
            if (!string.IsNullOrEmpty(config.Username) && !string.IsNullOrEmpty(config.Password)) {
                await client.AuthenticateAsync(config.Username, config.Password, ct);
            }
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
            return null;
        } catch (Exception ex) {
            return ex.Message;
        }
    }
}
