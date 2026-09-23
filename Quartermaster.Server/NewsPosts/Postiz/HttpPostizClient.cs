using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Quartermaster.Server.NewsPosts.Postiz;

public class HttpPostizClient : IPostizClient {
    private readonly HttpClient _http;
    private readonly NewsPostsSettings _settings;
    private readonly ILogger _logger;

    public HttpPostizClient(HttpClient http, NewsPostsSettings settings, ILogger? logger = null) {
        _http = http;
        _settings = settings;
        _logger = logger ?? NullLogger.Instance;
    }

    public async Task<List<PostizChannel>> GetChannelsAsync(CancellationToken ct) {
        using var request = CreateRequest(HttpMethod.Get, "/public/v1/integrations");
        using var doc = ParseStrict(await SendAsync(request, ct), request);
        try {
            return doc.RootElement
                .EnumerateArray()
                .Select(e => new PostizChannel(
                    e.GetProperty("id").GetString() ?? "",
                    StringOrNull(e, "name") ?? "",
                    StringOrNull(e, "identifier") ?? "",
                    StringOrNull(e, "profile"),
                    StringOrNull(e, "picture"),
                    e.TryGetProperty("disabled", out var disabled) && disabled.ValueKind == JsonValueKind.True))
                .ToList();
        } catch (Exception ex) when (ex is KeyNotFoundException || ex is InvalidOperationException || ex is IndexOutOfRangeException) {
            throw new PostizException("Postiz integrations answer has an unexpected shape: " + ex.Message);
        }
    }

    /// <summary>Returns the Postiz post ids. Never throws once Postiz has answered with a 2xx; an unreadable or unexpected success body yields an empty list.</summary>
    public async Task<List<string>> CreatePostAsync(string text, IReadOnlyList<PostizChannel> channels, DateTime? scheduledForUtc, CancellationToken ct) {
        var body = PostizRequestBuilder.Build(text, channels, scheduledForUtc, DateTime.UtcNow, _settings.XReplySetting);
        using var request = CreateRequest(HttpMethod.Post, "/public/v1/posts");
        request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        var responseBody = await SendAsync(request, ct);

        // After a 2xx the post may already be public; throwing here would make the caller record a failure and invite a repost.

        JsonDocument doc;
        try {
            doc = JsonDocument.Parse(responseBody);
        } catch (JsonException ex) {
            _logger.LogWarning(ex, "Postiz create-post answered 2xx with a non-JSON body: {Body}", Truncate(responseBody));
            return [];
        }

        using (doc) {
            if (doc.RootElement.ValueKind != JsonValueKind.Array) {
                _logger.LogWarning("Postiz create-post answer was not an array: {Body}", Truncate(responseBody));
                return [];
            }

            return doc.RootElement
                .EnumerateArray()
                .Select(PostIdOrEmpty)
                .Where(id => id.Length > 0)
                .ToList();
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path) {
        var request = new HttpRequestMessage(method, _settings.PostizBaseUrl + path);
        request.Headers.TryAddWithoutValidation("Authorization", _settings.PostizApiKey);
        return request;
    }

    /// <summary>Returns the body of a 2xx answer, or an empty string when that body cannot be read; throws <see cref="PostizException"/> for timeouts and non-success statuses.</summary>
    private async Task<string> SendAsync(HttpRequestMessage request, CancellationToken ct) {
        HttpResponseMessage response;
        try {
            response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        } catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
            throw new PostizException($"Postiz {request.RequestUri?.AbsolutePath} timed out");
        }
        using (response) {
            string text;
            try {
                text = await response.Content.ReadAsStringAsync(ct);
            } catch (Exception ex) when (IsBodyReadFailure(ex, ct)) {
                if (!response.IsSuccessStatusCode)
                    throw new PostizException($"Postiz {request.RequestUri?.AbsolutePath} returned {(int)response.StatusCode} with an unreadable body");

                // The status line already confirmed success; callers must not treat the request as failed.
                _logger.LogWarning(ex, "Postiz {Path} answered {Status} but the body could not be read", request.RequestUri?.AbsolutePath, (int)response.StatusCode);
                return "";
            }

            if (!response.IsSuccessStatusCode)
                throw new PostizException($"Postiz {request.RequestUri?.AbsolutePath} returned {(int)response.StatusCode}: {Truncate(text)}");
            return text;
        }
    }

    private static bool IsBodyReadFailure(Exception ex, CancellationToken ct) {
        if (ex is OperationCanceledException)
            return !ct.IsCancellationRequested;
        if (ex is HttpRequestException)
            return true;
        return ex is IOException;
    }

    private static JsonDocument ParseStrict(string text, HttpRequestMessage request) {
        try {
            return JsonDocument.Parse(text);
        } catch (JsonException ex) {
            throw new PostizException($"Postiz {request.RequestUri?.AbsolutePath} returned a non-JSON body: {ex.Message}");
        }
    }

    private static string PostIdOrEmpty(JsonElement element) {
        if (element.ValueKind != JsonValueKind.Object)
            return "";
        if (!element.TryGetProperty("postId", out var postId))
            return "";
        if (postId.ValueKind != JsonValueKind.String)
            return "";
        return postId.GetString() ?? "";
    }

    private static string Truncate(string text) => text.Length <= 300 ? text : text[..300];

    private static string? StringOrNull(JsonElement element, string name) {
        if (!element.TryGetProperty(name, out var value)) {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }
}
