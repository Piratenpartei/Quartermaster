using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Quartermaster.Server.NewsPosts.Mistral;

public class HttpMistralClient : IMistralClient {
    private const int MaxAttempts = 3;

    private readonly HttpClient _http;
    private readonly NewsPostsSettings _settings;
    private readonly ILogger _logger;

    public HttpMistralClient(HttpClient http, NewsPostsSettings settings, ILogger? logger = null) {
        _http = http;
        _settings = settings;
        _logger = logger ?? NullLogger.Instance;
    }

    public async Task<List<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken ct) {
        if (inputs.Count == 0)
            return [];

        var body = new { model = _settings.EmbeddingModel, input = inputs };
        using var doc = await PostAsync("/v1/embeddings", body, _settings.EmbeddingModel, ct);
        try {
            return doc.RootElement.GetProperty("data")
                .EnumerateArray()
                .OrderBy(e => e.GetProperty("index").GetInt32())
                .Select(e => e.GetProperty("embedding").EnumerateArray().Select(v => v.GetSingle()).ToArray())
                .ToList();
        } catch (Exception ex) when (ex is KeyNotFoundException || ex is JsonException || ex is InvalidOperationException || ex is IndexOutOfRangeException) {
            throw new MistralException("Mistral embeddings answer has an unexpected shape: " + ex.Message);
        }
    }

    public async Task<string> CompleteJsonAsync(string model, string systemPrompt, string userPrompt, CancellationToken ct) {
        var body = new {
            model,
            temperature = 0.4,
            response_format = new { type = "json_object" },
            messages = new[] {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        };
        using var doc = await PostAsync("/v1/chat/completions", body, model, ct);
        try {
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        } catch (Exception ex) when (ex is KeyNotFoundException || ex is JsonException || ex is InvalidOperationException || ex is IndexOutOfRangeException) {
            throw new MistralException("Mistral chat completion answer has an unexpected shape: " + ex.Message);
        }
    }

    public async Task<List<string>> ListModelsAsync(CancellationToken ct) {
        using var request = new HttpRequestMessage(HttpMethod.Get, _settings.MistralBaseUrl + "/v1/models");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.MistralApiKey);

        HttpResponseMessage response;
        try {
            response = await _http.SendAsync(request, ct);
        } catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
            throw new MistralException("Mistral /v1/models timed out");
        }
        using (response) {
            var text = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                throw new MistralException($"Mistral /v1/models returned {(int)response.StatusCode}: {Truncate(text)}");

            try {
                using var doc = JsonDocument.Parse(text);
                return doc.RootElement.GetProperty("data")
                    .EnumerateArray()
                    .Select(m => m.GetProperty("id").GetString() ?? "")
                    .Where(id => id.Length > 0)
                    .Distinct()
                    .Order(StringComparer.Ordinal)
                    .ToList();
            } catch (Exception ex) when (ex is KeyNotFoundException || ex is JsonException || ex is InvalidOperationException) {
                throw new MistralException("Mistral model list has an unexpected shape: " + ex.Message);
            }
        }
    }

    private async Task<JsonDocument> PostAsync(string path, object body, string model, CancellationToken ct) {
        for (var attempt = 1; ; attempt++) {
            var sw = Stopwatch.StartNew();
            using var request = new HttpRequestMessage(HttpMethod.Post, _settings.MistralBaseUrl + path) {
                Content = JsonContent.Create(body)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.MistralApiKey);

            HttpResponseMessage response;
            try {
                response = await _http.SendAsync(request, ct);
            } catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
                throw new MistralException($"Mistral {path} timed out");
            }
            using (response) {
                var text = await response.Content.ReadAsStringAsync(ct);
                if (response.IsSuccessStatusCode) {
                    JsonDocument doc;
                    try {
                        doc = JsonDocument.Parse(text);
                    } catch (JsonException ex) {
                        throw new MistralException($"Mistral {path} returned a non-JSON body: {ex.Message}");
                    }
                    LogUsage(path, model, doc, sw.ElapsedMilliseconds);
                    return doc;
                }

                var status = (int)response.StatusCode;
                if (!IsRetryable(status) || attempt >= MaxAttempts)
                    throw new MistralException($"Mistral {path} returned {status}: {Truncate(text)}");

                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
            }
        }
    }

    private static bool IsRetryable(int status) => status == 429 || status >= 500;

    private void LogUsage(string path, string model, JsonDocument doc, long elapsedMs) {
        var tokens = doc.RootElement.TryGetProperty("usage", out var usage) ? usage.ToString() : "{}";
        _logger.LogInformation("Mistral {Path} model={Model} usage={Usage} durationMs={Duration}", path, model, tokens, elapsedMs);
    }

    private static string Truncate(string text) => text.Length <= 300 ? text : text[..300];
}
