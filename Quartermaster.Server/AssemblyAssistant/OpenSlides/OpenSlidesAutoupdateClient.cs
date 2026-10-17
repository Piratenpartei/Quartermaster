using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartermaster.Server.AssemblyMotions.OpenSlides;

namespace Quartermaster.Server.AssemblyAssistant.OpenSlides;

/// <summary>
/// Long-lived autoupdate subscription of one meeting. Uses the login of the shared <see cref="IOpenSlidesClient"/>, reconnects
/// with backoff until stopped and starts every connection with a fresh key store, since the first line is the full map.
/// </summary>
public class OpenSlidesAutoupdateClient : IAsyncDisposable {
    private const string RefreshCookieName = "refreshId";

    private readonly HttpClient _http;
    private readonly IOpenSlidesClient _client;
    private readonly ILogger<OpenSlidesAutoupdateClient> _logger;
    private readonly OpenSlidesAutoupdateTimings _timings;

    private CancellationTokenSource? runCts;
    private Task? runTask;

    public event Action<OpenSlidesMeetingView>? Changed;

    /// <summary>True once the first line of a connection arrived; false with an error text when it dropped.</summary>
    public event Action<bool, string?>? ConnectionChanged;

    public OpenSlidesAutoupdateClient(HttpClient http, IOpenSlidesClient client, ILogger<OpenSlidesAutoupdateClient> logger, OpenSlidesAutoupdateTimings timings) {
        _http = http;
        _http.Timeout = Timeout.InfiniteTimeSpan;
        _client = client;
        _logger = logger;
        _timings = timings;
    }

    public Task StartAsync(OpenSlidesSettings settings, int meetingId, CancellationToken ct) {
        if (runTask != null)
            throw new InvalidOperationException("The autoupdate client is already running.");

        runCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        runTask = Task.Run(() => RunAsync(settings, meetingId, runCts.Token));
        return Task.CompletedTask;
    }

    public async Task StopAsync() {
        if (runCts == null || runTask == null)
            return;

        await runCts.CancelAsync();
        try {
            await runTask;
        } catch (OperationCanceledException) {
        }

        runCts.Dispose();
        runCts = null;
        runTask = null;
    }

    public async ValueTask DisposeAsync() {
        await StopAsync();
        GC.SuppressFinalize(this);
    }

    private async Task RunAsync(OpenSlidesSettings settings, int meetingId, CancellationToken ct) {
        var attempt = 0;
        while (!ct.IsCancellationRequested) {
            var connected = false;
            try {
                connected = await StreamAsync(settings, meetingId, () => connected = true, ct);
                ConnectionChanged?.Invoke(false, "stream ended");
            } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
                return;
            } catch (Exception ex) {
                _logger.LogWarning(ex, "OpenSlides autoupdate stream of meeting {MeetingId} failed", meetingId);
                ConnectionChanged?.Invoke(false, ex.Message);
            }

            attempt = connected ? 0 : attempt + 1;
            var delay = _timings.ReconnectBackoff[Math.Min(attempt, _timings.ReconnectBackoff.Count - 1)];
            try {
                await Task.Delay(delay, ct);
            } catch (OperationCanceledException) {
                return;
            }
        }
    }

    private async Task<bool> StreamAsync(OpenSlidesSettings settings, int meetingId, Action onConnected, CancellationToken ct) {
        var tokens = await _client.GetSessionTokensAsync(settings, ct);
        using var response = await SendAsync(settings, meetingId, tokens, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized) {
            // The stored token was rejected; a fresh login is used by the next attempt.
            await _client.LoginAsync(settings, ct);
            throw new OpenSlidesException("OpenSlides hat das Token des Autoupdates abgelehnt.", "unauthorized");
        }
        if (!response.IsSuccessStatusCode)
            throw new OpenSlidesException($"OpenSlides-Autoupdate antwortete mit HTTP {(int)response.StatusCode}.", "http_error");

        if (response.Headers.TryGetValues("Authentication", out var refreshed)) {
            var token = refreshed.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(token))
                _client.ReportRefreshedToken(token);
        }

        var store = new OpenSlidesKeyStore();
        var connected = false;
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        while (true) {
            var line = await ReadLineAsync(reader, ct);
            if (line == null)
                return connected;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            using var document = JsonDocument.Parse(line);
            if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("error", out var error))
                throw new OpenSlidesException($"OpenSlides-Autoupdate meldet einen Fehler: {error}", "action_failed");

            if (!connected) {
                connected = true;
                onConnected();
                ConnectionChanged?.Invoke(true, null);
            }
            if (store.Apply(document.RootElement))
                Changed?.Invoke(OpenSlidesMeetingView.From(store, meetingId));
        }
    }

    private async Task<HttpResponseMessage> SendAsync(OpenSlidesSettings settings, int meetingId, OpenSlidesSessionTokens tokens, CancellationToken ct) {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.Url.TrimEnd('/')}/system/autoupdate") {
            Content = new StringContent(OpenSlidesSubscription.Build(meetingId), Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("Authentication", tokens.AccessToken);
        if (tokens.RefreshId != null)
            request.Headers.TryAddWithoutValidation("Cookie", $"{RefreshCookieName}={tokens.RefreshId}");

        return await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
    }

    private async Task<string?> ReadLineAsync(StreamReader reader, CancellationToken ct) {
        using var silence = CancellationTokenSource.CreateLinkedTokenSource(ct);
        silence.CancelAfter(_timings.SilenceTimeout);
        try {
            return await reader.ReadLineAsync(silence.Token);
        } catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
            throw new OpenSlidesException("OpenSlides-Autoupdate ist verstummt.", "timeout");
        }
    }
}
