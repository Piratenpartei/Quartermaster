using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Quartermaster.Server.AssemblyMotions.OpenSlides;

/// <summary>
/// OpenSlides 4 action client. Every failure surfaces as an <see cref="OpenSlidesException"/> with a <see cref="OpenSlidesException.Code"/>;
/// transient errors are not retried, except one new login after HTTP 401.
/// </summary>
public class HttpOpenSlidesClient : IOpenSlidesClient, IDisposable {
    private const int MaxBodyExcerpt = 500;
    private const string RefreshCookieName = "refreshId";

    private readonly HttpClient _http;
    private readonly ILogger<HttpOpenSlidesClient> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private (string Url, string Username, string Password)? loggedInAs;
    private string? accessToken;
    private string? refreshId;

    public HttpOpenSlidesClient(HttpClient http, ILogger<HttpOpenSlidesClient> logger) {
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(30);
        _logger = logger;
    }

    public async Task LoginAsync(OpenSlidesSettings settings, CancellationToken ct) {
        await _gate.WaitAsync(ct);
        try {
            await LoginCoreAsync(settings, ct);
        } catch (Exception ex) when (ex is not OperationCanceledException) {
            Log(ex, "OpenSlides login failed");
            throw;
        } finally {
            _gate.Release();
        }
    }

    public async Task<int> CreateCategoryAsync(OpenSlidesSettings settings, int meetingId, string name, string prefix, CancellationToken ct) {
        var data = new Dictionary<string, object?> {
            ["meeting_id"] = meetingId,
            ["name"] = name,
            ["prefix"] = prefix
        };

        await _gate.WaitAsync(ct);
        try {
            using var doc = await ExecuteActionAsync(settings, "motion_category.create", data, ct);
            return ReadCreatedId(doc.RootElement);
        } catch (Exception ex) when (ex is not OperationCanceledException) {
            Log(ex, "OpenSlides category creation failed");
            throw;
        } finally {
            _gate.Release();
        }
    }

    public async Task<int> CreateMotionAsync(OpenSlidesSettings settings, OpenSlidesMotionCreate motion, CancellationToken ct) {
        var data = new Dictionary<string, object?> {
            ["meeting_id"] = motion.MeetingId,
            ["title"] = motion.Title,
            ["number"] = motion.Number,
            ["text"] = motion.TextHtml,
            ["category_id"] = motion.CategoryId,
            ["additional_submitter"] = motion.AdditionalSubmitter
        };
        AddReason(data, motion.ReasonHtml);

        await _gate.WaitAsync(ct);
        try {
            using var doc = await ExecuteActionAsync(settings, "motion.create", data, ct);
            return ReadCreatedId(doc.RootElement);
        } catch (Exception ex) when (ex is not OperationCanceledException) {
            Log(ex, "OpenSlides motion creation failed");
            throw;
        } finally {
            _gate.Release();
        }
    }

    public async Task UpdateMotionAsync(OpenSlidesSettings settings, OpenSlidesMotionUpdate motion, CancellationToken ct) {
        var data = new Dictionary<string, object?> {
            ["id"] = motion.MotionId,
            ["title"] = motion.Title,
            ["text"] = motion.TextHtml,
            ["additional_submitter"] = motion.AdditionalSubmitter
        };
        AddReason(data, motion.ReasonHtml);

        await _gate.WaitAsync(ct);
        try {
            using var doc = await ExecuteActionAsync(settings, "motion.update", data, ct);
        } catch (Exception ex) when (ex is not OperationCanceledException) {
            Log(ex, "OpenSlides update of motion failed");
            throw;
        } finally {
            _gate.Release();
        }
    }

    public void Dispose() {
        _http.Dispose();
        _gate.Dispose();
    }

    private void Log(Exception ex, string message) {
        if (ex is OpenSlidesException) {
            _logger.LogWarning(ex, "{Message}", message);
        } else {
            _logger.LogError(ex, "{Message}", message);
        }
    }

    private static void AddReason(Dictionary<string, object?> data, string? reasonHtml) {
        if (!string.IsNullOrEmpty(reasonHtml))
            data["reason"] = reasonHtml;
    }

    private static OpenSlidesException Unexpected(string detail) {
        return new($"Unerwartete Antwort von OpenSlides: {detail}", "unexpected_response");
    }

    private static string Excerpt(string body) {
        return body.Length <= MaxBodyExcerpt ? body : body[..MaxBodyExcerpt] + "...";
    }

    private static string TrimUrl(string url) {
        return url.TrimEnd('/');
    }

    private static int ReadCreatedId(JsonElement root) {
        if (!root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0)
            throw Unexpected("'results' is not a non-empty array");

        var first = results[0];
        if (first.ValueKind != JsonValueKind.Array || first.GetArrayLength() == 0)
            throw Unexpected("'results[0]' is not a non-empty array");

        var entry = first[0];
        if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty("id", out var id))
            throw Unexpected("'results[0][0].id' is missing");
        if (id.ValueKind != JsonValueKind.Number || !id.TryGetInt32(out var value))
            throw Unexpected("'results[0][0].id' is not an integer");

        return value;
    }

    private static void EnsureUsable(OpenSlidesSettings settings) {
        if (!settings.IsConfigured())
            throw new OpenSlidesException("Anmeldung an OpenSlides fehlgeschlagen: Zugang ist nicht konfiguriert.", "login_failed");
        if (!OpenSlidesSettings.IsSecureUrl(settings.Url))
            throw new OpenSlidesException("Die OpenSlides-URL muss https verwenden.", "insecure_url");
    }

    private static void CheckSuccess(JsonElement root) {
        if (!root.TryGetProperty("success", out var success) || (success.ValueKind != JsonValueKind.True && success.ValueKind != JsonValueKind.False))
            throw Unexpected("'success' is missing or not a boolean");
        if (success.ValueKind == JsonValueKind.True)
            return;

        var message = root.TryGetProperty("message", out var messageElement) && messageElement.ValueKind == JsonValueKind.String ? messageElement.GetString() : null;
        throw new OpenSlidesException(string.IsNullOrWhiteSpace(message) ? "OpenSlides hat die Aktion abgelehnt." : Excerpt(message), "action_failed");
    }

    private async Task<JsonDocument> ExecuteActionAsync(OpenSlidesSettings settings, string action, Dictionary<string, object?> data, CancellationToken ct) {
        EnsureUsable(settings);
        if (accessToken == null || loggedInAs != (settings.Url, settings.Username, settings.Password))
            await LoginCoreAsync(settings, ct);

        var payload = JsonSerializer.Serialize(new[] {
            new Dictionary<string, object?> {
                ["action"] = action,
                ["data"] = new[] { data }
            }
        });

        var response = await SendActionAsync(settings, payload, ct);
        if (response.Status == HttpStatusCode.Unauthorized) {
            _logger.LogInformation("OpenSlides rejected the token, logging in again");
            await LoginCoreAsync(settings, ct);
            response = await SendActionAsync(settings, payload, ct);
            if (response.Status == HttpStatusCode.Unauthorized)
                throw new OpenSlidesException("OpenSlides hat die Anmeldung abgelehnt.", "unauthorized");
        }

        if (response.Authentication != null)
            accessToken = response.Authentication;
        if (!response.IsSuccess) {
            var message = TryReadMessage(response.Body);
            if (message != null)
                throw new OpenSlidesException(Excerpt(message), "action_failed");

            throw new OpenSlidesException($"OpenSlides antwortete mit HTTP {(int)response.Status}: {Excerpt(response.Body)}", "http_error");
        }

        var doc = ParseObject(response.Body);
        try {
            CheckSuccess(doc.RootElement);
        } catch {
            doc.Dispose();
            throw;
        }

        return doc;
    }

    private static string? TryReadMessage(string body) {
        try {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;
            if (!doc.RootElement.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.String)
                return null;

            var value = message.GetString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        } catch (JsonException) {
            return null;
        }
    }

    private static JsonDocument ParseObject(string body) {
        JsonDocument doc;
        try {
            doc = JsonDocument.Parse(body);
        } catch (JsonException ex) {
            throw new OpenSlidesException($"OpenSlides lieferte kein gültiges JSON: {Excerpt(body)}", "unexpected_response", ex);
        }

        if (doc.RootElement.ValueKind != JsonValueKind.Object) {
            doc.Dispose();
            throw Unexpected("JSON root is not an object");
        }

        return doc;
    }

    private async Task LoginCoreAsync(OpenSlidesSettings settings, CancellationToken ct) {
        EnsureUsable(settings);
        accessToken = null;
        refreshId = null;
        loggedInAs = null;

        var payload = JsonSerializer.Serialize(new Dictionary<string, string> {
            ["username"] = settings.Username,
            ["password"] = settings.Password
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{TrimUrl(settings.Url)}/system/auth/login") {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        var response = await SendAsync(request, ct);
        if (response.Status == HttpStatusCode.Unauthorized || response.Status == HttpStatusCode.Forbidden)
            throw new OpenSlidesException("Anmeldung an OpenSlides fehlgeschlagen.", "login_failed");
        if (!response.IsSuccess)
            throw new OpenSlidesException($"OpenSlides antwortete mit HTTP {(int)response.Status}: {Excerpt(response.Body)}", "http_error");
        if (string.IsNullOrWhiteSpace(response.Authentication))
            throw new OpenSlidesException("Anmeldung an OpenSlides fehlgeschlagen.", "login_failed");

        accessToken = response.Authentication;
        refreshId = response.RefreshId;
        loggedInAs = (settings.Url, settings.Username, settings.Password);
        _logger.LogInformation("Logged in to OpenSlides as '{Username}'", settings.Username);
    }

    private async Task<OpenSlidesResponse> SendActionAsync(OpenSlidesSettings settings, string payload, CancellationToken ct) {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{TrimUrl(settings.Url)}/system/action/handle_request") {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("Authentication", accessToken);
        if (refreshId != null)
            request.Headers.TryAddWithoutValidation("Cookie", $"{RefreshCookieName}={refreshId}");

        return await SendAsync(request, ct);
    }

    private async Task<OpenSlidesResponse> SendAsync(HttpRequestMessage request, CancellationToken ct) {
        try {
            using var response = await _http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            return new OpenSlidesResponse(response.StatusCode, body, ReadAuthentication(response), ReadRefreshId(response));
        } catch (HttpRequestException ex) {
            throw new OpenSlidesException($"OpenSlides ist nicht erreichbar: {ex.Message}", "http_error", ex);
        } catch (OperationCanceledException ex) when (!ct.IsCancellationRequested) {
            throw new OpenSlidesException("Zeitüberschreitung bei der Anfrage an OpenSlides.", "timeout", ex);
        }
    }

    private static string? ReadAuthentication(HttpResponseMessage response) {
        if (!response.Headers.TryGetValues("Authentication", out var values))
            return null;

        var value = values.FirstOrDefault();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? ReadRefreshId(HttpResponseMessage response) {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
            return null;

        var prefix = RefreshCookieName + "=";
        var value = cookies
            .Where(c => c.StartsWith(prefix, StringComparison.Ordinal))
            .Select(c => c[prefix.Length..].Split(';')[0].Trim())
            .LastOrDefault(v => v.Length > 0);

        return value != null && value.All(IsCookieSafe) ? value : null;
    }

    private static bool IsCookieSafe(char c) {
        if (c == ',')
            return false;
        if (c == ';')
            return false;
        return c > 0x20 && c < 0x7f;
    }

    private record OpenSlidesResponse(HttpStatusCode Status, string Body, string? Authentication, string? RefreshId) {
        public bool IsSuccess => (int)Status >= 200 && (int)Status < 300;
    }
}
