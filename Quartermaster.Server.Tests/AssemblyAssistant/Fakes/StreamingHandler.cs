using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Server.Tests.AssemblyMotions.Fakes;

namespace Quartermaster.Server.Tests.AssemblyAssistant.Fakes;

/// <summary>Answers requests from a queue; a response marked keepOpen streams its lines and then stays open until the request is cancelled.</summary>
public class StreamingHandler : HttpMessageHandler {
    private readonly Lock _lock = new();
    private readonly Queue<(HttpStatusCode Status, string[] Lines, bool KeepOpen, string? Authentication)> _responses = new();
    private readonly List<RecordedOpenSlidesRequest> _requests = [];

    public IReadOnlyList<RecordedOpenSlidesRequest> Requests {
        get {
            using (_lock.EnterScope()) {
                return [.. _requests];
            }
        }
    }

    public void Enqueue(HttpStatusCode status, string[] lines, bool keepOpen, string? authentication = null) {
        using (_lock.EnterScope()) {
            _responses.Enqueue((status, lines, keepOpen, authentication));
        }
    }

    public async Task WaitForRequestsAsync(int count) {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (Requests.Count < count) {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Expected {count} requests, saw {Requests.Count}.");
            await Task.Delay(20);
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        var body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        (HttpStatusCode Status, string[] Lines, bool KeepOpen, string? Authentication) next;
        using (_lock.EnterScope()) {
            _requests.Add(new RecordedOpenSlidesRequest(request.Method, request.RequestUri!.ToString(),
                request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value)), body));
            next = _responses.Count > 0 ? _responses.Dequeue() : (HttpStatusCode.ServiceUnavailable, [], false, null);
        }

        var content = Encoding.UTF8.GetBytes(string.Join("\n", next.Lines) + (next.Lines.Length > 0 ? "\n" : ""));
        var stream = new LineStream(content, next.KeepOpen, cancellationToken);
        var response = new HttpResponseMessage(next.Status) { Content = new StreamContent(stream) };
        if (next.Authentication != null)
            response.Headers.TryAddWithoutValidation("Authentication", next.Authentication);
        return response;
    }
}
