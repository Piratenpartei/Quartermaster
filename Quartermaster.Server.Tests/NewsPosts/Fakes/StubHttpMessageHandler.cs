using System.Net;
using System.Text;

namespace Quartermaster.Server.Tests.NewsPosts.Fakes;

public class StubHttpMessageHandler : HttpMessageHandler {
    private readonly Queue<Func<HttpResponseMessage>> _responses = new();

    public List<HttpRequestMessage> Requests { get; } = [];
    public List<string> RequestBodies { get; } = [];

    public void Enqueue(HttpStatusCode status, string body, string mediaType = "application/json") {
        _responses.Enqueue(() => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, mediaType) });
    }

    /// <summary>Makes the next send throw, so tests can simulate a timeout or transport failure.</summary>
    public void EnqueueException(Exception exception) {
        _responses.Enqueue(() => throw exception);
    }

    /// <summary>Answers with <paramref name="status"/> but fails while the body is read, like a timeout after the headers arrived.</summary>
    public void EnqueueFailingBody(HttpStatusCode status, Exception exception) {
        _responses.Enqueue(() => new HttpResponseMessage(status) { Content = new FailingContent(exception) });
    }

    /// <summary>Answers with <paramref name="status"/>, an empty body and a Location header, like Keycloak's create endpoints.</summary>
    public void EnqueueWithLocation(HttpStatusCode status, string location) {
        _responses.Enqueue(() => {
            var response = new HttpResponseMessage(status) { Content = new StringContent("") };
            response.Headers.Location = new Uri(location);
            return response;
        });
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        Requests.Add(request);
        RequestBodies.Add(request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
        return _responses.Dequeue()();
    }

    private sealed class FailingContent : HttpContent {
        private readonly Exception _exception;

        public FailingContent(Exception exception) {
            _exception = exception;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => throw _exception;

        protected override bool TryComputeLength(out long length) {
            length = 0;
            return false;
        }
    }
}
