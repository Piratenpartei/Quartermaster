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

namespace Quartermaster.Server.AssemblyMotions.Forum;

/// <summary>
/// Discourse REST client. Requests never follow redirects on their own; only the external id lookup follows one redirect to the same host.
/// Response bodies are never logged, so neither the API key nor member data from a post ends up in the log.
/// </summary>
public class HttpDiscourseClient : IDiscourseClient, IDisposable {
    private readonly HttpClient _http;
    private readonly ILogger<HttpDiscourseClient> _logger;

    public HttpDiscourseClient(HttpClient http, ILogger<HttpDiscourseClient> logger) {
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(30);
        _logger = logger;
    }

    public async Task<DiscourseCurrentUser> GetCurrentUserAsync(DiscourseSettings settings, CancellationToken ct) {
        using var doc = await SendAsync(settings, HttpMethod.Get, "/session/current.json", null, ct);
        var user = Property(doc.RootElement, "current_user");
        return new DiscourseCurrentUser(
            StringProperty(user, "username"),
            BoolProperty(user, "moderator") || BoolProperty(user, "admin"));
    }

    public async Task<DiscourseCategory> GetCategoryAsync(DiscourseSettings settings, int categoryId, CancellationToken ct) {
        using var doc = await SendAsync(settings, HttpMethod.Get, $"/c/{categoryId}/show.json", null, ct);
        var category = Property(doc.RootElement, "category");
        var permission = category.TryGetProperty("permission", out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : 0;
        return new DiscourseCategory(
            IntProperty(category, "id"),
            StringProperty(category, "name"),
            BoolProperty(category, "read_restricted"),
            permission == 1);
    }

    public async Task<DiscourseTopic?> FindTopicByExternalIdAsync(DiscourseSettings settings, string externalId, CancellationToken ct) {
        var path = $"/t/external_id/{Uri.EscapeDataString(externalId)}.json";
        using var response = await SendRawAsync(settings, HttpMethod.Get, Url(settings, path), null, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!IsRedirect(response.StatusCode)) {
            using var direct = await ReadAsync(response, ct);
            return ReadTopic(direct.RootElement);
        }

        var target = RedirectTarget(settings, response);
        using var redirected = await SendRawAsync(settings, HttpMethod.Get, target, null, ct);
        if (redirected.StatusCode == HttpStatusCode.NotFound)
            return null;

        using var doc = await ReadAsync(redirected, ct);
        return ReadTopic(doc.RootElement);
    }

    public async Task<DiscourseTopic> GetTopicAsync(DiscourseSettings settings, int topicId, CancellationToken ct) {
        using var doc = await SendAsync(settings, HttpMethod.Get, $"/t/{topicId}.json", null, ct);
        return ReadTopic(doc.RootElement);
    }

    public async Task<DiscoursePost> GetPostAsync(DiscourseSettings settings, int postId, CancellationToken ct) {
        using var doc = await SendAsync(settings, HttpMethod.Get, $"/posts/{postId}.json", null, ct);
        return new DiscoursePost(
            IntProperty(doc.RootElement, "id"),
            StringProperty(doc.RootElement, "raw"),
            IsSet(doc.RootElement, "deleted_at"));
    }

    public async Task<DiscourseTopic> CreateTopicAsync(DiscourseSettings settings, string title, string raw, int categoryId, string externalId, CancellationToken ct) {
        var body = new Dictionary<string, object> {
            ["title"] = title,
            ["raw"] = raw,
            ["category"] = categoryId,
            ["external_id"] = externalId
        };
        using var doc = await SendAsync(settings, HttpMethod.Post, "/posts.json", body, ct);
        return new DiscourseTopic(IntProperty(doc.RootElement, "topic_id"), IntProperty(doc.RootElement, "id"), title, false);
    }

    public async Task EditPostAsync(DiscourseSettings settings, int postId, string raw, string editReason, CancellationToken ct) {
        var body = new Dictionary<string, object> {
            ["post"] = new Dictionary<string, object> {
                ["raw"] = raw,
                ["edit_reason"] = editReason
            }
        };
        using var doc = await SendAsync(settings, HttpMethod.Put, $"/posts/{postId}.json", body, ct);
    }

    public async Task UpdateTopicTitleAsync(DiscourseSettings settings, int topicId, string title, CancellationToken ct) {
        var body = new Dictionary<string, object> { ["title"] = title };
        using var doc = await SendAsync(settings, HttpMethod.Put, $"/t/-/{topicId}.json", body, ct);
    }

    public void Dispose() {
        _http.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<JsonDocument> SendAsync(DiscourseSettings settings, HttpMethod method, string path, object? body, CancellationToken ct) {
        using var response = await SendRawAsync(settings, method, Url(settings, path), body, ct);
        if (IsRedirect(response.StatusCode))
            throw Fail(method, path, new DiscourseException("Das Forum hat unerwartet weitergeleitet.", DiscourseException.InvalidResponse, (int)response.StatusCode));

        return await ReadAsync(response, ct);
    }

    private async Task<HttpResponseMessage> SendRawAsync(DiscourseSettings settings, HttpMethod method, Uri url, object? body, CancellationToken ct) {
        if (!DiscourseSettings.IsSecureUrl(settings.BaseUrl))
            throw new DiscourseException("Die Forum-URL muss https verwenden.", DiscourseException.InsecureUrl);

        using var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("Api-Key", settings.ApiKey);
        request.Headers.TryAddWithoutValidation("Api-Username", settings.ApiUsername);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        if (body != null)
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try {
            response = await _http.SendAsync(request, ct);
        } catch (TaskCanceledException ex) when (!ct.IsCancellationRequested) {
            throw Fail(method, url.AbsolutePath, new DiscourseException("Zeitüberschreitung bei der Anfrage an das Forum.", DiscourseException.Timeout, inner: ex));
        } catch (HttpRequestException ex) {
            throw Fail(method, url.AbsolutePath, new DiscourseException("Das Forum ist nicht erreichbar.", DiscourseException.HttpError, inner: ex));
        }

        if (IsRedirect(response.StatusCode) || response.StatusCode == HttpStatusCode.NotFound || response.IsSuccessStatusCode)
            return response;

        using (response) {
            throw Fail(method, url.AbsolutePath, await ErrorAsync(settings, response, ct));
        }
    }

    private static async Task<JsonDocument> ReadAsync(HttpResponseMessage response, CancellationToken ct) {
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new DiscourseException("Im Forum nicht gefunden.", DiscourseException.NotFound, 404);

        var text = await response.Content.ReadAsStringAsync(ct);
        try {
            return JsonDocument.Parse(text.Length == 0 ? "{}" : text);
        } catch (JsonException ex) {
            throw new DiscourseException("Das Forum hat unerwartet geantwortet.", DiscourseException.InvalidResponse, (int)response.StatusCode, inner: ex);
        }
    }

    private static async Task<DiscourseException> ErrorAsync(DiscourseSettings settings, HttpResponseMessage response, CancellationToken ct) {
        var status = (int)response.StatusCode;
        if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            return new DiscourseException("Das Forum hat den API-Schlüssel oder das Bot-Konto abgelehnt.", DiscourseException.Unauthorized, status);

        if (status == 429)
            return new DiscourseException("Das Forum begrenzt die Anfragen gerade.", DiscourseException.RateLimited, status, RetryAfter(response));

        if (status == 422) {
            var errors = await ErrorsAsync(response, ct);
            var message = errors.Count == 0 ? "Das Forum hat den Beitrag abgelehnt." : string.Join(" ", errors);
            return new DiscourseException(Redact(settings, message), DiscourseException.Rejected, status);
        }

        return new DiscourseException("Das Forum ist nicht erreichbar oder hat die Anfrage abgelehnt.", DiscourseException.HttpError, status);
    }

    private static async Task<List<string>> ErrorsAsync(HttpResponseMessage response, CancellationToken ct) {
        try {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Array)
                return [];

            return errors.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)
                .ToList();
        } catch (JsonException) {
            return [];
        }
    }

    private static TimeSpan? RetryAfter(HttpResponseMessage response) {
        var delta = response.Headers.RetryAfter?.Delta;
        if (delta != null)
            return delta;
        if (response.Headers.TryGetValues("Retry-After", out var values) && int.TryParse(values.FirstOrDefault(), out var seconds))
            return TimeSpan.FromSeconds(seconds);
        return null;
    }

    private static string Redact(DiscourseSettings settings, string text) {
        return settings.ApiKey.Length == 0 ? text : text.Replace(settings.ApiKey, "***");
    }

    private DiscourseException Fail(HttpMethod method, string path, DiscourseException exception) {
        _logger.LogWarning("Discourse request {Method} {Path} failed: {Code} (HTTP {Status})", method, path, exception.Code, exception.StatusCode);
        return exception;
    }

    private static Uri Url(DiscourseSettings settings, string path) {
        return new Uri(settings.BaseUrl + path);
    }

    private static bool IsRedirect(HttpStatusCode status) {
        var code = (int)status;
        return code >= 300 && code < 400;
    }

    private static Uri RedirectTarget(DiscourseSettings settings, HttpResponseMessage response) {
        var baseUri = new Uri(settings.BaseUrl + "/");
        var location = response.Headers.Location;
        if (location == null)
            throw new DiscourseException("Das Forum hat ohne Ziel weitergeleitet.", DiscourseException.InvalidResponse, (int)response.StatusCode);

        var target = location.IsAbsoluteUri ? location : new Uri(baseUri, location);
        if (!string.Equals(target.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase) || target.Scheme != baseUri.Scheme)
            throw new DiscourseException("Das Forum hat auf einen fremden Server weitergeleitet.", DiscourseException.InvalidResponse, (int)response.StatusCode);

        return target;
    }

    private static DiscourseTopic ReadTopic(JsonElement topic) {
        var posts = Property(Property(topic, "post_stream"), "posts");
        if (posts.ValueKind != JsonValueKind.Array || posts.GetArrayLength() == 0)
            throw new DiscourseException("Das Forum hat ein Thema ohne Beiträge geliefert.", DiscourseException.InvalidResponse);

        return new DiscourseTopic(
            IntProperty(topic, "id"),
            IntProperty(posts[0], "id"),
            StringProperty(topic, "title"),
            IsSet(topic, "deleted_at"));
    }

    private static JsonElement Property(JsonElement element, string name) {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
            throw new DiscourseException($"Das Forum hat unerwartet geantwortet (Feld {name} fehlt).", DiscourseException.InvalidResponse);
        return value;
    }

    private static int IntProperty(JsonElement element, string name) {
        var value = Property(element, name);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number))
            throw new DiscourseException($"Das Forum hat unerwartet geantwortet (Feld {name}).", DiscourseException.InvalidResponse);
        return number;
    }

    private static string StringProperty(JsonElement element, string name) {
        var value = Property(element, name);
        return value.ValueKind == JsonValueKind.String ? value.GetString()! : "";
    }

    private static bool BoolProperty(JsonElement element, string name) {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
    }

    private static bool IsSet(JsonElement element, string name) {
        return element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null && value.ValueKind != JsonValueKind.Undefined;
    }
}
