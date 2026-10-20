using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
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
    private readonly bool _allowLoopbackHttp;
    private readonly ILogger<HttpOpenSlidesClient> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private (string Url, string Username, string Password)? loggedInAs;
    private string? accessToken;
    private string? refreshId;

    public HttpOpenSlidesClient(HttpClient http, IHostEnvironment environment, ILogger<HttpOpenSlidesClient> logger) {
        _http = http;
        _allowLoopbackHttp = environment.IsDevelopment();
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

    public async Task<OpenSlidesSessionTokens> GetSessionTokensAsync(OpenSlidesSettings settings, CancellationToken ct) {
        await _gate.WaitAsync(ct);
        try {
            EnsureUsable(settings);
            if (accessToken == null || loggedInAs != (settings.Url, settings.Username, settings.Password))
                await LoginCoreAsync(settings, ct);

            return new OpenSlidesSessionTokens(accessToken!, refreshId);
        } catch (Exception ex) when (ex is not OperationCanceledException) {
            Log(ex, "OpenSlides login for the autoupdate stream failed");
            throw;
        } finally {
            _gate.Release();
        }
    }

    public void ReportRefreshedToken(string authentication) {
        _gate.Wait();
        try {
            accessToken = authentication;
        } finally {
            _gate.Release();
        }
    }

    public async Task<int> CreatePollAsync(OpenSlidesSettings settings, OpenSlidesPollCreate poll, CancellationToken ct) {
        var options = poll.Options.Select(OptionData).ToList();
        var data = new Dictionary<string, object?> {
            ["meeting_id"] = poll.MeetingId,
            ["title"] = poll.Title,
            ["type"] = "pseudoanonymous",
            ["pollmethod"] = poll.PollMethod,
            ["content_object_id"] = poll.ContentObjectId,
            ["options"] = options,
            ["entitled_group_ids"] = new[] { poll.EntitledGroupId },
            ["min_votes_amount"] = poll.MinVotes,
            ["max_votes_amount"] = poll.MaxVotes,
            ["max_votes_per_option"] = poll.MaxVotesPerOption,
            ["global_yes"] = poll.GlobalYes,
            ["global_no"] = poll.GlobalNo,
            ["global_abstain"] = poll.GlobalAbstain,
            ["onehundred_percent_base"] = poll.OnehundredPercentBase,
            ["backend"] = "fast"
        };

        using var doc = await RunActionAsync(settings, "poll.create", data, "OpenSlides poll creation failed", ct);
        return ReadCreatedId(doc.RootElement);
    }

    public Task StartPollAsync(OpenSlidesSettings settings, int pollId, CancellationToken ct) {
        return RunIdActionAsync(settings, "poll.start", pollId, ct);
    }

    public Task StopPollAsync(OpenSlidesSettings settings, int pollId, CancellationToken ct) {
        return RunIdActionAsync(settings, "poll.stop", pollId, ct);
    }

    public Task PublishPollAsync(OpenSlidesSettings settings, int pollId, CancellationToken ct) {
        return RunIdActionAsync(settings, "poll.publish", pollId, ct);
    }

    public Task ResetPollAsync(OpenSlidesSettings settings, int pollId, CancellationToken ct) {
        return RunIdActionAsync(settings, "poll.reset", pollId, ct);
    }

    public Task DeletePollAsync(OpenSlidesSettings settings, int pollId, CancellationToken ct) {
        return RunIdActionAsync(settings, "poll.delete", pollId, ct);
    }

    public async Task UpdateCountdownAsync(OpenSlidesSettings settings, int countdownId, bool running, double countdownTime, CancellationToken ct) {
        var data = new Dictionary<string, object?> {
            ["id"] = countdownId,
            ["running"] = running,
            ["countdown_time"] = countdownTime
        };

        using var doc = await RunActionAsync(settings, "projector_countdown.update", data, "OpenSlides countdown update failed", ct);
    }

    public async Task<int> CreateTopicAsync(OpenSlidesSettings settings, int meetingId, string title, int agendaParentId, CancellationToken ct) {
        var data = new Dictionary<string, object?> {
            ["meeting_id"] = meetingId,
            ["title"] = title,
            ["agenda_parent_id"] = agendaParentId,
            ["agenda_type"] = "common"
        };

        using var doc = await RunActionAsync(settings, "topic.create", data, "OpenSlides topic creation failed", ct);
        return ReadCreatedId(doc.RootElement);
    }

    public async Task ProjectAsync(OpenSlidesSettings settings, int meetingId, IReadOnlyList<int> projectorIds, string contentObjectId, string? type, CancellationToken ct) {
        var data = new Dictionary<string, object?> {
            ["ids"] = projectorIds,
            ["meeting_id"] = meetingId,
            ["content_object_id"] = contentObjectId
        };
        if (type != null)
            data["type"] = type;

        using var doc = await RunActionAsync(settings, "projector.project", data, "OpenSlides projection failed", ct);
    }

    public void Dispose() {
        _http.Dispose();
        _gate.Dispose();
    }

    private static Dictionary<string, object?> OptionData(OpenSlidesPollOption option) {
        if (option.ContentObjectId != null)
            return new Dictionary<string, object?> { ["content_object_id"] = option.ContentObjectId };

        return new Dictionary<string, object?> { ["text"] = option.Text };
    }

    private async Task RunIdActionAsync(OpenSlidesSettings settings, string action, int id, CancellationToken ct) {
        var data = new Dictionary<string, object?> { ["id"] = id };
        using var doc = await RunActionAsync(settings, action, data, $"OpenSlides action {action} failed", ct);
    }

    private async Task<JsonDocument> RunActionAsync(OpenSlidesSettings settings, string action, Dictionary<string, object?> data, string failureMessage, CancellationToken ct) {
        await _gate.WaitAsync(ct);
        try {
            return await ExecuteActionAsync(settings, action, data, ct);
        } catch (Exception ex) when (ex is not OperationCanceledException) {
            Log(ex, failureMessage);
            throw;
        } finally {
            _gate.Release();
        }
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

    private void EnsureUsable(OpenSlidesSettings settings) {
        if (!settings.IsConfigured())
            throw new OpenSlidesException("Anmeldung an OpenSlides fehlgeschlagen: Zugang ist nicht konfiguriert.", "login_failed");
        if (!OpenSlidesSettings.IsSecureUrl(settings.Url, _allowLoopbackHttp))
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
