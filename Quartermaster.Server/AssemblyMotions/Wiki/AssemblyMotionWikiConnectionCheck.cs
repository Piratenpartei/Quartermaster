using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.AssemblyMotions.Wiki;

public class AssemblyMotionWikiConnectionCheck {
    public const string NotConfiguredMessage = "Die Wiki-Zugangsdaten sind nicht hinterlegt.";

    private const string LoginFailedPrefix = "Anmeldung am Wiki fehlgeschlagen: ";

    private readonly IWikiBotClient _wiki;
    private readonly OptionRepository _options;
    private readonly ILogger<AssemblyMotionWikiConnectionCheck> _logger;

    public AssemblyMotionWikiConnectionCheck(IWikiBotClient wiki, OptionRepository options, ILogger<AssemblyMotionWikiConnectionCheck> logger) {
        _wiki = wiki;
        _options = options;
        _logger = logger;
    }

    /// <summary>Makes exactly one login attempt with the stored settings and never reads, edits or protects a page.</summary>
    public async Task<WikiConnectionCheckResult> CheckAsync(CancellationToken ct) {
        var settings = WikiBotSettings.ReadFrom(_options);
        if (!settings.IsConfigured())
            return new WikiConnectionCheckResult(false, false, NotConfiguredMessage);

        try {
            await _wiki.LoginAsync(settings, ct);
        } catch (Exception ex) when (!ct.IsCancellationRequested) {
            _logger.LogWarning("Wiki connection check failed: {Exception}", ex);
            return new WikiConnectionCheckResult(false, true, Reason(ex));
        }

        return new WikiConnectionCheckResult(true, true, $"Verbindung erfolgreich: angemeldet als {settings.Username}.");
    }

    // Exception messages from the client can contain an excerpt of the response body, so only fixed texts and the wiki's own login reason pass through.
    private static string Reason(Exception exception) {
        if (exception is HttpRequestException)
            return "Das Wiki ist nicht erreichbar.";
        if (exception is TimeoutException)
            return "Zeitüberschreitung bei der Anfrage an das Wiki.";
        if (exception is not WikiBotException wikiException)
            return "Unerwarteter Fehler, Details stehen im Serverprotokoll.";

        switch (wikiException.Code) {
            case "login_failed":
                return wikiException.Message.StartsWith(LoginFailedPrefix)
                    ? wikiException.Message[LoginFailedPrefix.Length..]
                    : wikiException.Message;
            case "insecure_url":
                return "Die Wiki-API-URL muss https verwenden.";
            case "blocked_html":
                return "Das Wiki hat die Anfrage blockiert (HTML statt JSON).";
            case "timeout":
                return "Zeitüberschreitung bei der Anfrage an das Wiki.";
            case "http_error":
                return "Das Wiki ist nicht erreichbar oder hat die Anfrage abgelehnt.";
            default:
                return "Das Wiki hat unerwartet geantwortet.";
        }
    }
}
