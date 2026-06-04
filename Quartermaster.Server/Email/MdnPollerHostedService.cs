using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MimeKit;
using Quartermaster.Data.Notifications;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.Email;

/// <summary>Polls the configured IMAP mailbox for inbound delivery feedback - Message Disposition Notifications
/// (RFC 3798) and Delivery Status Notifications (RFC 3464 bounces) - joins them back to the originating
/// <see cref="NotificationLog"/> via the captured outbound Message-Id, and records the outcome on that row.</summary>
public class MdnPollerHostedService : BackgroundService {
    private const string UnknownFolderName = "mdn-unknown";

    private readonly IServiceProvider _services;
    private readonly ILogger<MdnPollerHostedService> _logger;

    public MdnPollerHostedService(IServiceProvider services, ILogger<MdnPollerHostedService> logger) {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        try {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        } catch (OperationCanceledException) {
            return;
        }

        while (!stoppingToken.IsCancellationRequested) {
            var interval = TimeSpan.FromMinutes(15);
            try {
                interval = await PollOnceAsync(stoppingToken);
            } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                return;
            } catch (Exception ex) {
                _logger.LogError(ex, "MDN poll cycle failed");
            }

            try {
                await Task.Delay(interval, stoppingToken);
            } catch (OperationCanceledException) {
                return;
            }
        }
    }

    private async Task<TimeSpan> PollOnceAsync(CancellationToken ct) {
        using var scope = _services.CreateScope();
        var optionRepo = scope.ServiceProvider.GetRequiredService<OptionRepository>();
        var config = ImapConfig.ReadFrom(optionRepo);
        if (config == null) {
            _logger.LogDebug("IMAP not configured, skipping poll");
            return TimeSpan.FromMinutes(15);
        }
        var logRepo = scope.ServiceProvider.GetRequiredService<NotificationLogRepository>();

        using var client = new ImapClient();
        await client.ConnectAsync(config.Host, config.Port,
            config.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, ct);
        await client.AuthenticateAsync(config.Username, config.Password, ct);

        var inbox = await OpenMailboxAsync(client, config.Mailbox, ct);
        var unknownFolder = await GetOrCreateFolderAsync(client, UnknownFolderName, ct);

        var unread = await inbox.SearchAsync(SearchQuery.NotSeen, ct);
        var matchedMdn = 0;
        var matchedDsn = 0;
        var unmatched = 0;
        foreach (var uid in unread) {
            if (ct.IsCancellationRequested)
                break;

            var message = await inbox.GetMessageAsync(uid, ct);
            var reportType = ReportType(message);
            if (reportType == ReportKind.None) {
                continue;
            }

            string? originalMessageId;
            string? mdnDisposition = null;
            string? bounceDiagnostic = null;
            string? parseError;
            if (reportType == ReportKind.Mdn) {
                (originalMessageId, mdnDisposition, parseError) = ParseMdn(message);
            } else {
                (originalMessageId, bounceDiagnostic, parseError) = ParseDsn(message);
            }
            if (parseError != null) {
                _logger.LogWarning("Skipping malformed report uid={Uid} kind={Kind}: {Error}", uid, reportType, parseError);
                continue;
            }

            var log = string.IsNullOrEmpty(originalMessageId)
                ? null
                : logRepo.FindByMessageId(originalMessageId!);
            if (log == null) {
                unmatched++;
                if (unknownFolder != null) {
                    await inbox.MoveToAsync(uid, unknownFolder, ct);
                } else {
                    await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true, ct);
                }
                continue;
            }

            if (reportType == ReportKind.Mdn) {
                logRepo.RecordMdnReceived(log.Id, mdnDisposition ?? "unknown", DateTime.UtcNow);
                matchedMdn++;
            } else {
                logRepo.RecordBounce(log.Id, bounceDiagnostic ?? "unknown", DateTime.UtcNow);
                matchedDsn++;
            }
            await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true, ct);
        }

        await client.DisconnectAsync(true, ct);
        if (matchedMdn > 0 || matchedDsn > 0 || unmatched > 0) {
            _logger.LogInformation("MDN poll matched {Mdn} MDN, {Dsn} DSN; {Unmatched} unmatched moved aside",
                matchedMdn, matchedDsn, unmatched);
        }
        return TimeSpan.FromMinutes(config.PollIntervalMinutes);
    }

    private static async Task<IMailFolder> OpenMailboxAsync(ImapClient client, string name, CancellationToken ct) {
        var folder = name.Equals("INBOX", StringComparison.OrdinalIgnoreCase)
            ? client.Inbox
            : await client.GetFolderAsync(name, ct);
        await folder.OpenAsync(FolderAccess.ReadWrite, ct);
        return folder;
    }

    private async Task<IMailFolder?> GetOrCreateFolderAsync(ImapClient client, string name, CancellationToken ct) {
        try {
            return await client.GetFolderAsync(name, ct);
        } catch (FolderNotFoundException) {
            try {
                var root = client.GetFolder(client.PersonalNamespaces[0]);
                return await root.CreateAsync(name, isMessageFolder: true, ct);
            } catch (Exception ex) {
                _logger.LogWarning(ex, "Could not create IMAP folder '{Name}'; marking as Seen instead", name);
                return null;
            }
        }
    }

    private enum ReportKind { None, Mdn, Dsn }

    private static ReportKind ReportType(MimeMessage message) {
        if (message.Body is not Multipart multipart)
            return ReportKind.None;
        if (!string.Equals(multipart.ContentType.MediaSubtype, "report", StringComparison.OrdinalIgnoreCase))
            return ReportKind.None;
        var reportType = multipart.ContentType.Parameters["report-type"];
        if (string.Equals(reportType, "disposition-notification", StringComparison.OrdinalIgnoreCase))
            return ReportKind.Mdn;
        if (string.Equals(reportType, "delivery-status", StringComparison.OrdinalIgnoreCase))
            return ReportKind.Dsn;
        return ReportKind.None;
    }

    private static (string? OriginalMessageId, string? Disposition, string? Error) ParseMdn(MimeMessage message) {
        var (text, error) = ReadReportPart(message, "disposition-notification");
        if (error != null)
            return (null, null, error);

        string? originalMessageId = null;
        string? disposition = null;
        foreach (var rawLine in text!.Split('\n')) {
            var line = rawLine.TrimEnd('\r');
            if (line.StartsWith("Original-Message-ID:", StringComparison.OrdinalIgnoreCase)) {
                originalMessageId = line["Original-Message-ID:".Length..].Trim().Trim('<', '>');
            } else if (line.StartsWith("Disposition:", StringComparison.OrdinalIgnoreCase)) {
                var raw = line["Disposition:".Length..].Trim();
                var semi = raw.IndexOf(';');
                if (semi >= 0) {
                    var typePart = raw[(semi + 1)..].Trim();
                    var slash = typePart.IndexOf('/');
                    disposition = slash >= 0 ? typePart[..slash].Trim() : typePart;
                }
            }
        }
        return (originalMessageId, disposition, null);
    }

    /// <summary>
    /// Parses the <c>message/delivery-status</c> part for the per-recipient Action / Status / Diagnostic-Code
    /// fields, and pulls the original Message-Id out of the <c>message/rfc822</c> (or
    /// <c>text/rfc822-headers</c>) part the DSN includes by RFC 3464.
    /// </summary>
    private static (string? OriginalMessageId, string? Diagnostic, string? Error) ParseDsn(MimeMessage message) {
        var (statusText, statusError) = ReadReportPart(message, "delivery-status");
        if (statusError != null)
            return (null, null, statusError);

        string? action = null;
        string? status = null;
        string? diagnosticCode = null;
        foreach (var rawLine in statusText!.Split('\n')) {
            var line = rawLine.TrimEnd('\r');
            if (line.StartsWith("Action:", StringComparison.OrdinalIgnoreCase)) {
                action = line["Action:".Length..].Trim();
            } else if (line.StartsWith("Status:", StringComparison.OrdinalIgnoreCase)) {
                status = line["Status:".Length..].Trim();
            } else if (line.StartsWith("Diagnostic-Code:", StringComparison.OrdinalIgnoreCase)) {
                diagnosticCode = line["Diagnostic-Code:".Length..].Trim();
            }
        }

        if (!string.Equals(action, "failed", StringComparison.OrdinalIgnoreCase)) {
            return (null, null, $"non-failure action '{action ?? "(none)"}'");
        }

        var originalMessageId = ExtractOriginalMessageIdFromBounce(message);
        var diagnostic = BuildDiagnostic(action, status, diagnosticCode);
        return (originalMessageId, diagnostic, null);
    }

    private static (string? Text, string? Error) ReadReportPart(MimeMessage message, string mediaSubtype) {
        if (message.Body is not Multipart multipart)
            return (null, "body is not multipart");

        var msgPart = multipart.OfType<MessagePart>().FirstOrDefault(p =>
            string.Equals(p.ContentType.MediaType, "message", StringComparison.OrdinalIgnoreCase)
            && string.Equals(p.ContentType.MediaSubtype, mediaSubtype, StringComparison.OrdinalIgnoreCase));
        if (msgPart != null) {
            using var ms = new MemoryStream();
            msgPart.Message.Headers.WriteTo(MimeKit.FormatOptions.Default, ms);
            return (System.Text.Encoding.UTF8.GetString(ms.ToArray()), null);
        }
        var mimePart = multipart.OfType<MimePart>().FirstOrDefault(p =>
            string.Equals(p.ContentType.MediaType, "message", StringComparison.OrdinalIgnoreCase)
            && string.Equals(p.ContentType.MediaSubtype, mediaSubtype, StringComparison.OrdinalIgnoreCase));
        if (mimePart != null) {
            using var ms = new MemoryStream();
            mimePart.Content.DecodeTo(ms);
            return (System.Text.Encoding.UTF8.GetString(ms.ToArray()), null);
        }
        return (null, $"no message/{mediaSubtype} part");
    }

    private static string? ExtractOriginalMessageIdFromBounce(MimeMessage message) {
        if (message.Body is not Multipart multipart)
            return null;

        var originalMsg = multipart.OfType<MessagePart>().FirstOrDefault(p =>
            string.Equals(p.ContentType.MediaType, "message", StringComparison.OrdinalIgnoreCase)
            && string.Equals(p.ContentType.MediaSubtype, "rfc822", StringComparison.OrdinalIgnoreCase));
        if (originalMsg != null) {
            var id = originalMsg.Message.MessageId;
            if (!string.IsNullOrEmpty(id))
                return id.Trim().Trim('<', '>');
        }

        var headersPart = multipart.OfType<MimePart>().FirstOrDefault(p =>
            string.Equals(p.ContentType.MediaType, "text", StringComparison.OrdinalIgnoreCase)
            && string.Equals(p.ContentType.MediaSubtype, "rfc822-headers", StringComparison.OrdinalIgnoreCase));
        if (headersPart == null) {
            return null;
        }
        using var ms = new MemoryStream();
        headersPart.Content.DecodeTo(ms);
        var text = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        foreach (var rawLine in text.Split('\n')) {
            var line = rawLine.TrimEnd('\r');
            if (line.StartsWith("Message-ID:", StringComparison.OrdinalIgnoreCase)) {
                return line["Message-ID:".Length..].Trim().Trim('<', '>');
            }
        }
        return null;
    }

    private static string BuildDiagnostic(string? action, string? status, string? diagnosticCode) {
        var parts = new System.Collections.Generic.List<string>();
        if (!string.IsNullOrEmpty(action))
            parts.Add(action!);
        if (!string.IsNullOrEmpty(status))
            parts.Add(status!);
        if (!string.IsNullOrEmpty(diagnosticCode))
            parts.Add(diagnosticCode!);
        if (parts.Count == 0)
            return "bounced";
        var joined = string.Join("; ", parts);
        return joined.Length > 1000 ? joined[..1000] : joined;
    }
}
