using System.Net;
using System.Text;
using System.Text.Json;

namespace Quartermaster.Server.Tests.AssemblyMotions.Fakes;

/// <summary>
/// Serves canned responses per key (the action; token queries are "tokens-login" and "tokens-csrf", content reads are "query")
/// and records every request with decoded form fields.
/// </summary>
public class RecordingWikiHandler : HttpMessageHandler {
    private readonly Lock _lock = new();
    private readonly Dictionary<string, Queue<(HttpStatusCode Status, string Body)>> _queues = [];
    private readonly List<RecordedWikiRequest> _requests = [];

    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    public List<RecordedWikiRequest> Requests {
        get {
            using (_lock.EnterScope()) {
                return [.. _requests];
            }
        }
    }

    public List<string> Actions => Requests.Select(r => r.Action).ToList();

    public void Enqueue(string key, string body, HttpStatusCode status = HttpStatusCode.OK) {
        using (_lock.EnterScope()) {
            if (!_queues.TryGetValue(key, out var queue)) {
                queue = new Queue<(HttpStatusCode, string)>();
                _queues[key] = queue;
            }

            queue.Enqueue((status, body));
        }
    }

    public void EnqueueLogin(string loginToken = "lt+\\", string csrfToken = "csrf+\\") {
        Enqueue("tokens-login", """{"query":{"tokens":{"logintoken":""" + JsonSerializer.Serialize(loginToken) + "}}}");
        Enqueue("login", """{"login":{"result":"Success","lguserid":1,"lgusername":"Bot"}}""");
        Enqueue("tokens-csrf", """{"query":{"tokens":{"csrftoken":""" + JsonSerializer.Serialize(csrfToken) + "}}}");
    }

    public static Dictionary<string, string> ParseForm(string encoded) {
        var fields = new Dictionary<string, string>();
        foreach (var pair in encoded.Split('&', StringSplitOptions.RemoveEmptyEntries)) {
            var index = pair.IndexOf('=');
            fields[Uri.UnescapeDataString(pair[..index].Replace('+', ' '))] = Uri.UnescapeDataString(pair[(index + 1)..].Replace('+', ' '));
        }

        return fields;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        var encoded = request.Method == HttpMethod.Post
            ? await request.Content!.ReadAsStringAsync(cancellationToken)
            : request.RequestUri!.Query.TrimStart('?');
        var fields = ParseForm(encoded);
        var headers = request.Headers.ToDictionary(h => h.Key, h => string.Join(" ", h.Value));
        var recorded = new RecordedWikiRequest(request.Method, request.RequestUri!.AbsoluteUri, fields, headers, encoded);

        if (Delay > TimeSpan.Zero) {
            await Task.Delay(Delay, cancellationToken);
        }

        var key = KeyOf(fields);
        (HttpStatusCode Status, string Body) answer;
        using (_lock.EnterScope()) {
            _requests.Add(recorded);
            if (!_queues.TryGetValue(key, out var queue) || queue.Count == 0) {
                throw new InvalidOperationException($"No canned response left for '{key}'.");
            }

            answer = queue.Dequeue();
        }

        return new HttpResponseMessage(answer.Status) {
            Content = new StringContent(answer.Body, Encoding.UTF8, "application/json")
        };
    }

    private static string KeyOf(Dictionary<string, string> fields) {
        var action = fields.GetValueOrDefault("action", "");
        if (action != "query") {
            return action;
        }

        if (fields.GetValueOrDefault("meta") == "tokens") {
            return "tokens-" + fields.GetValueOrDefault("type", "csrf");
        }

        return "query";
    }
}
