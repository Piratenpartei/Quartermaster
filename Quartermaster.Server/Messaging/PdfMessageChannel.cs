using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.Messaging;

/// <summary>
/// Renders the message to a PDF in the configured output dir — the "channel" is the
/// postal-mail outbox folder. V1 layout: address top, subject H1, body 1:1 (envelope
/// layout + batch rendering tracked as a feature todo).
/// </summary>
public class PdfMessageChannel : IMessageChannel {
    public const string ChannelId = "pdf";
    private const string DefaultRelativeOutputDir = "data/printouts";

    private readonly OptionRepository _optionRepo;
    private readonly ILogger<PdfMessageChannel> _logger;

    public PdfMessageChannel(OptionRepository optionRepo, ILogger<PdfMessageChannel> logger) {
        _optionRepo = optionRepo;
        _logger = logger;
    }

    public string Id => ChannelId;

    /// <summary>Always true — falls back to a default dir under <see cref="AppContext.BaseDirectory"/> when unconfigured.</summary>
    public bool IsConfigured => true;

    public NotificationBodyFormat BodyFormat => NotificationBodyFormat.Html;

    public Task<ChannelDeliveryResult> SendAsync(ChannelMessage message, CancellationToken ct = default) {
        try {
            var dir = ResolveOutputDir();
            Directory.CreateDirectory(dir);

            var filename = BuildFilename(message);
            var fullPath = Path.Combine(dir, filename);

            var bytes = Render(message);
            File.WriteAllBytes(fullPath, bytes);

            _logger.LogInformation("Wrote PDF printout: {Path} ({Size} bytes)", fullPath, bytes.Length);
            return Task.FromResult(ChannelDeliveryResult.Ok());
        } catch (IOException ex) {
            _logger.LogError(ex, "PDF printout write failed for {Address}", message.ChannelAddress);
            return Task.FromResult(ChannelDeliveryResult.Fail($"PDF write failed: {ex.Message}"));
        } catch (UnauthorizedAccessException ex) {
            _logger.LogError(ex, "PDF printout output dir not writable");
            return Task.FromResult(ChannelDeliveryResult.Fail($"PDF output dir not writable: {ex.Message}"));
        }
    }

    private string ResolveOutputDir() {
        var configured = _optionRepo.GetGlobalValue("messaging.pdf.output_dir")?.Value;
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;
        return Path.Combine(AppContext.BaseDirectory, DefaultRelativeOutputDir);
    }

    private static string BuildFilename(ChannelMessage message) {
        var ts = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var slug = Slugify(message.ChannelAddress);
        var guid = Guid.NewGuid().ToString("N").Substring(0, 8);
        return $"{ts}-{slug}-{guid}.pdf";
    }

    private static string Slugify(string input) {
        if (string.IsNullOrWhiteSpace(input))
            return "unaddressed";
        var span = input.Length > 32 ? input.AsSpan(0, 32) : input.AsSpan();
        var sb = new System.Text.StringBuilder(span.Length);
        foreach (var c in span) {
            if (char.IsLetterOrDigit(c))
                sb.Append(char.ToLowerInvariant(c));
            else if (c == ' ' || c == '_' || c == '-' || c == '\n' || c == ',')
                sb.Append('_');
        }
        var result = sb.ToString().Trim('_');
        return string.IsNullOrEmpty(result) ? "unaddressed" : result;
    }

    private static byte[] Render(ChannelMessage message) {
        return Document.Create(container => {
            container.Page(page => {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(11).FontFamily("Helvetica"));

                page.Content().Column(col => {
                    if (!string.IsNullOrWhiteSpace(message.ChannelAddress)) {
                        col.Item().Text(message.ChannelAddress).FontSize(11);
                        col.Item().PaddingTop(1, Unit.Centimetre);
                    }

                    if (!string.IsNullOrWhiteSpace(message.Subject)) {
                        col.Item().Text(message.Subject).FontSize(16).Bold();
                        col.Item().PaddingTop(8);
                    }

                    col.Item().Text(message.Body ?? "").FontSize(11);
                });
            });
        }).GeneratePdf();
    }
}
