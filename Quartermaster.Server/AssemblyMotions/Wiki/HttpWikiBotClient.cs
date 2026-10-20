using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartermaster.Rendering;

namespace Quartermaster.Server.AssemblyMotions.Wiki;

/// <summary>
/// MediaWiki bot client. Every failure surfaces as a <see cref="WikiBotException"/> with a <see cref="WikiBotException.Code"/>;
/// transient errors are not retried.
/// </summary>
public class HttpWikiBotClient : IWikiBotClient, IDisposable {
    // The wiki's bot check challenges browser-like user agents, also on api.php; a plain tool UA gets JSON.
    private const string UserAgent = "Quartermaster/4 (+https://piratenpartei.de)";

    private const int MaxBodyExcerpt = 200;

    private readonly HttpClient _http;
    private readonly bool _allowLoopbackHttp;
    private readonly ILogger<HttpWikiBotClient> _logger;
    // One gate for the whole of every public call: login and token state are never raced, and the sync service handles pages one by one anyway.
    private readonly SemaphoreSlim _gate = new(1, 1);

    private (string ApiUrl, string Username, string Password)? loggedInAs;
    private string? csrfToken;
    private (string ApiUrl, string Pattern)? articlePath;

    public HttpWikiBotClient(HttpMessageHandler handler, IHostEnvironment environment, ILogger<HttpWikiBotClient> logger)
        : this(new HttpClient(handler), environment, logger) { }

    public HttpWikiBotClient(HttpClient http, IHostEnvironment environment, ILogger<HttpWikiBotClient> logger) {
        _http = http;
        _allowLoopbackHttp = environment.IsDevelopment();
        _http.Timeout = TimeSpan.FromSeconds(30);
        _logger = logger;
    }

    /// <summary>The primary handler of the "wiki" named client: keeps the session cookies and surfaces a 3xx answer as an http_error.</summary>
    public static SocketsHttpHandler CreateHandler() {
        // A followed 307 or 308 would resend the login POST with the bot password to the redirect target.
        return new SocketsHttpHandler {
            AllowAutoRedirect = false,
            UseCookies = true,
            CookieContainer = new CookieContainer(),
            PooledConnectionLifetime = TimeSpan.FromMinutes(15)
        };
    }

    public async Task<string> GetArticlePathAsync(WikiBotSettings settings, CancellationToken ct) {
        await _gate.WaitAsync(ct);
        try {
            if (articlePath != null && articlePath.Value.ApiUrl == settings.ApiUrl) {
                return articlePath.Value.Pattern;
            }

            // Site info is public, so no login: the link also works while the bot account is broken.
            using var doc = await SendAsync(settings, false, [
                new("action", "query"),
                new("meta", "siteinfo"),
                new("siprop", "general")
            ], ct);
            ThrowIfError(doc);

            var general = Member(doc.RootElement, "siteinfo", "query", "general");
            var server = StringMember(general, "siteinfo", "server");
            var path = StringMember(general, "siteinfo", "articlepath");
            if (!path.Contains("$1")) {
                throw Unexpected("siteinfo", "articlepath has no $1");
            }

            var pattern = (server.StartsWith("//") ? "https:" + server : server).TrimEnd('/') + path;
            articlePath = (settings.ApiUrl, pattern);
            return pattern;
        } catch (Exception ex) when (ex is not OperationCanceledException) {
            _logger.LogWarning(ex, "Wiki site info could not be read");
            throw;
        } finally {
            _gate.Release();
        }
    }

    public async Task LoginAsync(WikiBotSettings settings, CancellationToken ct) {
        await _gate.WaitAsync(ct);
        try {
            await LoginCoreAsync(settings, ct);
        } catch (Exception ex) when (ex is not OperationCanceledException) {
            _logger.LogWarning(ex, "Wiki login failed");
            throw;
        } finally {
            _gate.Release();
        }
    }

    public async Task<string?> GetContentAsync(WikiBotSettings settings, string title, CancellationToken ct) {
        title = TextNormalization.ToNfc(title);
        await _gate.WaitAsync(ct);
        try {
            using var doc = await ExecuteAsync(settings, false, _ => [
                new("action", "query"),
                new("prop", "revisions"),
                new("rvprop", "content"),
                new("rvslots", "main"),
                new("titles", title)
            ], ct);

            var pages = Member(doc.RootElement, "read", "query", "pages");
            if (pages.ValueKind != JsonValueKind.Array || pages.GetArrayLength() == 0) {
                throw Unexpected("read", "pages is not a non-empty array");
            }

            var page = pages[0];
            if (page.ValueKind != JsonValueKind.Object) {
                throw Unexpected("read", "page entry is not an object");
            }

            if (IsTrue(page, "missing")) {
                return null;
            }

            if (IsTrue(page, "invalid")) {
                throw new WikiBotException($"Ungültiger Seitentitel '{title}'.", "invalidtitle");
            }

            var revisions = Member(page, "read", "revisions");
            if (revisions.ValueKind != JsonValueKind.Array || revisions.GetArrayLength() == 0) {
                throw Unexpected("read", "page has no revisions");
            }

            var main = Member(revisions[0], "read", "slots", "main");
            if (IsTrue(main, "texthidden")) {
                throw Unexpected("read", "revision text is hidden");
            }

            return StringMember(main, "read", "content");
        } catch (Exception ex) when (ex is not OperationCanceledException) {
            _logger.LogWarning(ex, "Wiki read of '{Title}' failed", title);
            throw;
        } finally {
            _gate.Release();
        }
    }

    public async Task EditAsync(WikiBotSettings settings, string title, string text, string summary, CancellationToken ct) {
        title = TextNormalization.ToNfc(title);
        text = TextNormalization.ToNfc(text);
        summary = TextNormalization.ToNfc(summary);
        await _gate.WaitAsync(ct);
        try {
            using var doc = await ExecuteAsync(settings, true, token => [
                new("action", "edit"),
                new("title", title),
                new("text", text),
                new("summary", summary),
                new("bot", "1"),
                new("assert", "user"),
                new("token", token!)
            ], ct);

            var result = CheckEditResult(doc.RootElement);
            _logger.LogInformation("Wiki edit of '{Title}': {Result}", title, result);
        } catch (Exception ex) when (ex is not OperationCanceledException) {
            _logger.LogError(ex, "Wiki edit of '{Title}' failed", title);
            throw;
        } finally {
            _gate.Release();
        }
    }

    public async Task ProtectAsync(WikiBotSettings settings, string title, CancellationToken ct) {
        title = TextNormalization.ToNfc(title);
        await _gate.WaitAsync(ct);
        try {
            using var doc = await ExecuteAsync(settings, true, token => [
                new("action", "protect"),
                new("title", title),
                new("protections", $"edit={settings.ProtectionLevel}|move={settings.ProtectionLevel}"),
                new("expiry", "infinite|infinite"),
                new("assert", "user"),
                new("token", token!)
            ], ct);

            Member(doc.RootElement, "protect", "protect");

            _logger.LogInformation("Wiki protect of '{Title}' to level '{Level}': Success", title, settings.ProtectionLevel);
        } catch (Exception ex) when (ex is not OperationCanceledException) {
            _logger.LogError(ex, "Wiki protect of '{Title}' failed", title);
            throw;
        } finally {
            _gate.Release();
        }
    }

    public void Dispose() {
        _http.Dispose();
        _gate.Dispose();
    }

    private static string CheckEditResult(JsonElement root) {
        var edit = Member(root, "edit", "edit");
        if (edit.ValueKind != JsonValueKind.Object) {
            throw Unexpected("edit", "edit is not an object");
        }

        var result = edit.TryGetProperty("result", out var resultElement) && resultElement.ValueKind == JsonValueKind.String ? resultElement.GetString() ?? "" : "";
        if (result == "Success") {
            return IsTrue(edit, "nochange") ? "Success (nochange)" : "Success";
        }

        if (edit.TryGetProperty("captcha", out _)) {
            throw new WikiBotException("Speichern abgelehnt: Captcha verlangt.", "captcha");
        }

        if (edit.TryGetProperty("abusefilter", out var abuse)) {
            var description = abuse.ValueKind == JsonValueKind.Object && abuse.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null;
            throw new WikiBotException($"Speichern vom Missbrauchsfilter abgelehnt: {description ?? "unbekannter Filter"}", "abusefilter");
        }

        if (edit.TryGetProperty("spamblacklist", out var spam)) {
            throw new WikiBotException($"Speichern von der Spam-Blacklist abgelehnt: {Excerpt(spam.ToString())}", "spamblacklist");
        }

        throw new WikiBotException($"Speichern fehlgeschlagen: {(result.Length == 0 ? "unbekanntes Ergebnis" : result)}", "edit_failed");
    }

    private static WikiBotException Unexpected(string action, string detail) {
        return new($"Unerwartete Antwort des Wikis ({action}): {detail}", "unexpected_response");
    }

    private static bool IsTrue(JsonElement element, string name) {
        if (element.ValueKind != JsonValueKind.Object)
            return false;
        if (!element.TryGetProperty(name, out var value))
            return false;
        return value.ValueKind == JsonValueKind.True;
    }

    private static JsonElement Member(JsonElement element, string action, params string[] path) {
        var current = element;
        foreach (var name in path) {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(name, out var next)) {
                throw Unexpected(action, $"'{name}' is missing");
            }

            current = next;
        }

        return current;
    }

    private static string StringMember(JsonElement element, string action, string name) {
        var value = Member(element, action, name);
        if (value.ValueKind != JsonValueKind.String) {
            throw Unexpected(action, $"'{name}' is not a string");
        }

        return value.GetString() ?? "";
    }

    private static string Excerpt(string body) {
        return body.Length <= MaxBodyExcerpt ? body : body[..MaxBodyExcerpt] + "...";
    }

    private static string Encode(IEnumerable<KeyValuePair<string, string>> fields) {
        var builder = new StringBuilder();
        foreach (var field in fields) {
            if (builder.Length > 0) {
                builder.Append('&');
            }

            builder.Append(Uri.EscapeDataString(field.Key));
            builder.Append('=');
            builder.Append(Uri.EscapeDataString(field.Value));
        }

        return builder.ToString();
    }

    /// <summary>Runs one API call, logging in first when needed and once more after a stale session or token.</summary>
    private async Task<JsonDocument> ExecuteAsync(
        WikiBotSettings settings,
        bool post,
        Func<string?, KeyValuePair<string, string>[]> buildFields,
        CancellationToken ct) {
        await EnsureLoggedInAsync(settings, ct);
        for (var attempt = 0; ; attempt++) {
            var doc = await SendAsync(settings, post, buildFields(csrfToken), ct);
            if (attempt > 0 || !IsStaleSession(doc)) {
                ThrowIfError(doc);
                return doc;
            }

            doc.Dispose();
            _logger.LogInformation("Wiki session or token was rejected, logging in again");
            await LoginCoreAsync(settings, ct);
        }
    }

    private static bool IsStaleSession(JsonDocument doc) {
        if (!doc.RootElement.TryGetProperty("error", out var error)) {
            return false;
        }

        if (error.ValueKind != JsonValueKind.Object || !error.TryGetProperty("code", out var code)) {
            return false;
        }

        if (code.ValueKind != JsonValueKind.String) {
            return false;
        }

        var value = code.GetString();
        return value == "badtoken" || value == "assertuserfailed" || value == "notloggedin";
    }

    private static void ThrowIfError(JsonDocument doc) {
        if (!doc.RootElement.TryGetProperty("error", out var error)) {
            return;
        }

        if (error.ValueKind != JsonValueKind.Object) {
            doc.Dispose();
            throw Unexpected("error", "'error' is not an object");
        }

        var code = error.TryGetProperty("code", out var codeElement) && codeElement.ValueKind == JsonValueKind.String ? codeElement.GetString() : null;
        var info = error.TryGetProperty("info", out var infoElement) && infoElement.ValueKind == JsonValueKind.String ? infoElement.GetString() : null;
        var message = $"Wiki-Fehler ({code ?? "unbekannt"}): {Excerpt(info ?? "keine Details")}";
        doc.Dispose();
        throw new WikiBotException(message, code);
    }

    private async Task EnsureLoggedInAsync(WikiBotSettings settings, CancellationToken ct) {
        if (csrfToken != null && loggedInAs == (settings.ApiUrl, settings.Username, settings.Password)) {
            return;
        }

        await LoginCoreAsync(settings, ct);
    }

    private async Task LoginCoreAsync(WikiBotSettings settings, CancellationToken ct) {
        if (!settings.IsConfigured()) {
            throw new WikiBotException("Anmeldung am Wiki fehlgeschlagen: Bot-Zugang ist nicht konfiguriert.", "login_failed");
        }

        csrfToken = null;
        loggedInAs = null;

        using var tokenDoc = await SendAsync(settings, false, [
            new("action", "query"),
            new("meta", "tokens"),
            new("type", "login")
        ], ct);
        ThrowIfError(tokenDoc);
        var loginToken = ReadToken(tokenDoc, "logintoken");

        using var loginDoc = await SendAsync(settings, true, [
            new("action", "login"),
            new("lgname", settings.Username),
            new("lgpassword", settings.Password),
            new("lgtoken", loginToken)
        ], ct);
        ThrowIfError(loginDoc);
        CheckLoginResult(loginDoc.RootElement);

        using var csrfDoc = await SendAsync(settings, false, [
            new("action", "query"),
            new("meta", "tokens")
        ], ct);
        ThrowIfError(csrfDoc);
        csrfToken = ReadToken(csrfDoc, "csrftoken");
        loggedInAs = (settings.ApiUrl, settings.Username, settings.Password);
        _logger.LogInformation("Logged in to the wiki as '{Username}'", settings.Username);
    }

    private static void CheckLoginResult(JsonElement root) {
        var login = Member(root, "login", "login");
        var result = login.ValueKind == JsonValueKind.Object && login.TryGetProperty("result", out var resultElement) && resultElement.ValueKind == JsonValueKind.String
            ? resultElement.GetString()
            : null;
        if (result == "Success") {
            return;
        }

        var reason = login.ValueKind == JsonValueKind.Object && login.TryGetProperty("reason", out var reasonElement) && reasonElement.ValueKind == JsonValueKind.String
            ? reasonElement.GetString()
            : null;
        throw new WikiBotException($"Anmeldung am Wiki fehlgeschlagen: {Excerpt(reason ?? result ?? "unbekannt")}", "login_failed");
    }

    private static string ReadToken(JsonDocument doc, string name) {
        var tokens = Member(doc.RootElement, "token", "query", "tokens");
        var token = StringMember(tokens, "token", name);
        if (token.Length == 0) {
            throw Unexpected("token", $"'{name}' is empty");
        }

        return token;
    }

    private async Task<JsonDocument> SendAsync(WikiBotSettings settings, bool post, KeyValuePair<string, string>[] fields, CancellationToken ct) {
        EnsureSecureUrl(settings.ApiUrl);
        var all = new List<KeyValuePair<string, string>> {
            new("format", "json"),
            new("formatversion", "2")
        };
        all.AddRange(fields);

        // Built by hand: FormUrlEncodedContent can reject very long values, and tokens contain "+\" which must be escaped.
        using var request = post
            ? new HttpRequestMessage(HttpMethod.Post, settings.ApiUrl) {
                Content = new StringContent(Encode(all), Encoding.UTF8, "application/x-www-form-urlencoded")
            }
            : new HttpRequestMessage(HttpMethod.Get, $"{settings.ApiUrl}?{Encode(all)}");
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

        string body;
        try {
            using var response = await _http.SendAsync(request, ct);
            body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode) {
                throw new WikiBotException($"Das Wiki antwortete mit HTTP {(int)response.StatusCode}: {Excerpt(body)}", "http_error");
            }
        } catch (HttpRequestException ex) {
            throw new WikiBotException($"Das Wiki ist nicht erreichbar: {ex.Message}", "http_error", ex);
        } catch (OperationCanceledException ex) when (!ct.IsCancellationRequested) {
            throw new WikiBotException("Zeitüberschreitung bei der Anfrage an das Wiki.", "timeout", ex);
        }

        if (body.TrimStart().StartsWith('<')) {
            throw new WikiBotException("Das Wiki hat die Anfrage blockiert (HTML statt JSON).", "blocked_html");
        }

        JsonDocument doc;
        try {
            doc = JsonDocument.Parse(body);
        } catch (JsonException ex) {
            throw new WikiBotException($"Das Wiki lieferte kein gültiges JSON: {Excerpt(body)}", "invalid_json", ex);
        }

        if (doc.RootElement.ValueKind != JsonValueKind.Object) {
            doc.Dispose();
            throw Unexpected("response", "JSON root is not an object");
        }

        return doc;
    }

    private void EnsureSecureUrl(string apiUrl) {
        if (!WikiBotSettings.IsSecureUrl(apiUrl, _allowLoopbackHttp)) {
            throw new WikiBotException("Die Wiki-API-URL muss https verwenden.", "insecure_url");
        }
    }
}
