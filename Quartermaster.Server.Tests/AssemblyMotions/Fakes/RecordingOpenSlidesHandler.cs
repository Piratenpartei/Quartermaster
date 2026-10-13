using System.Net;
using System.Text;

namespace Quartermaster.Server.Tests.AssemblyMotions.Fakes;

public class RecordingOpenSlidesHandler : HttpMessageHandler {
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();
    private readonly List<RecordedOpenSlidesRequest> _requests = [];

    public IReadOnlyList<RecordedOpenSlidesRequest> Requests => _requests;

    public void EnqueueLogin(string token = "bearer token-1", string refreshId = "refresh-1") {
        Enqueue(HttpStatusCode.OK, """{"success":true}""", token, $"{refreshId};Path=/;HttpOnly");
    }

    public void Enqueue(HttpStatusCode status, string body, string? authentication = null, string? refreshCookieValue = null) {
        _responses.Enqueue(_ => {
            var response = new HttpResponseMessage(status) {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            if (authentication != null)
                response.Headers.TryAddWithoutValidation("Authentication", authentication);
            if (refreshCookieValue != null)
                response.Headers.TryAddWithoutValidation("Set-Cookie", $"refreshId={refreshCookieValue}");

            return response;
        });
    }

    public void EnqueueOk(string resultsJson, string? authentication = null)
        => Enqueue(HttpStatusCode.OK, $$"""{"success":true,"message":"Action handled successfully","results":{{resultsJson}}}""", authentication);

    public void EnqueueThrow(Exception exception) {
        _responses.Enqueue(_ => throw exception);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        var body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        _requests.Add(new RecordedOpenSlidesRequest(
            request.Method,
            request.RequestUri!.ToString(),
            request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value)),
            body));
        if (_responses.Count == 0)
            throw new InvalidOperationException("No response queued.");

        return _responses.Dequeue()(request);
    }
}

public record RecordedOpenSlidesRequest(HttpMethod Method, string Url, Dictionary<string, string> Headers, string Body);
