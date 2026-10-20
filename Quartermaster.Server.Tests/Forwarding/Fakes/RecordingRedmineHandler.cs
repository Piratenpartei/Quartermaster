using System.Net;
using System.Text;

namespace Quartermaster.Server.Tests.Forwarding.Fakes;

public class RecordingRedmineHandler : HttpMessageHandler {
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();
    private readonly List<RecordedRedmineRequest> _requests = [];

    public IReadOnlyList<RecordedRedmineRequest> Requests => _requests;

    public void Enqueue(HttpStatusCode status, string body, Dictionary<string, string>? headers = null) {
        _responses.Enqueue(_ => {
            var response = new HttpResponseMessage(status) {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            foreach (var header in headers ?? []) {
                response.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return response;
        });
    }

    public void EnqueueOk(string body) {
        Enqueue(HttpStatusCode.OK, body);
    }

    public void EnqueueRedirect(string location) {
        Enqueue(HttpStatusCode.MovedPermanently, "", new Dictionary<string, string> { ["Location"] = location });
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        var body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        _requests.Add(new RecordedRedmineRequest(
            request.Method,
            request.RequestUri!.ToString(),
            request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value)),
            body));
        if (_responses.Count == 0)
            throw new InvalidOperationException("No response queued.");

        return _responses.Dequeue()(request);
    }
}

public record RecordedRedmineRequest(HttpMethod Method, string Url, Dictionary<string, string> Headers, string Body);
