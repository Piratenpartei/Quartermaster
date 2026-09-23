using System.Net;
using System.Text.Json;
using Quartermaster.Server.NewsPosts;
using Quartermaster.Server.NewsPosts.Mistral;
using Quartermaster.Server.Tests.NewsPosts.Fakes;

namespace Quartermaster.Server.Tests.NewsPosts;

public class HttpMistralClientTests {
    private static NewsPostsSettings Settings() => new() {
        MistralApiKey = "key",
        MistralBaseUrl = "https://mistral.test",
        EmbeddingModel = "mistral-embed"
    };

    [Test]
    public async Task EmbedAsync_posts_batch_and_returns_vectors_in_input_order() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, """
            {"data":[{"index":1,"embedding":[0.5,0.5]},{"index":0,"embedding":[1.0,0.0]}],"usage":{"prompt_tokens":4,"total_tokens":4}}
            """);
        var client = new HttpMistralClient(new HttpClient(handler), Settings());

        var vectors = await client.EmbedAsync(["a", "b"], CancellationToken.None);

        await Assert.That(vectors[0]).IsEquivalentTo(new[] { 1.0f, 0.0f });
        await Assert.That(vectors[1]).IsEquivalentTo(new[] { 0.5f, 0.5f });
        await Assert.That(handler.Requests[0].RequestUri!.ToString()).IsEqualTo("https://mistral.test/v1/embeddings");
        await Assert.That(handler.Requests[0].Headers.Authorization!.ToString()).IsEqualTo("Bearer key");
        using var body = JsonDocument.Parse(handler.RequestBodies[0]);
        await Assert.That(body.RootElement.GetProperty("model").GetString()).IsEqualTo("mistral-embed");
    }

    [Test]
    public async Task CompleteJsonAsync_requests_json_mode_and_returns_message_content() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, """
            {"choices":[{"message":{"role":"assistant","content":"{\"relevant\":true,\"reason\":\"x\"}"}}],"usage":{"prompt_tokens":10,"completion_tokens":5}}
            """);
        var client = new HttpMistralClient(new HttpClient(handler), Settings());

        var content = await client.CompleteJsonAsync("mistral-small-latest", "sys", "user", CancellationToken.None);

        await Assert.That(content).IsEqualTo("{\"relevant\":true,\"reason\":\"x\"}");
        using var body = JsonDocument.Parse(handler.RequestBodies[0]);
        await Assert.That(body.RootElement.GetProperty("response_format").GetProperty("type").GetString()).IsEqualTo("json_object");
        await Assert.That(body.RootElement.GetProperty("messages").GetArrayLength()).IsEqualTo(2);
    }

    [Test]
    public async Task Success_response_with_unexpected_shape_throws_MistralException() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, """{"unexpected":true}""");
        var client = new HttpMistralClient(new HttpClient(handler), Settings());

        await Assert.ThrowsAsync<MistralException>(() => client.CompleteJsonAsync("m", "s", "u", CancellationToken.None));
    }

    [Test]
    public async Task Success_response_with_non_json_body_throws_MistralException() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, "not json");
        var client = new HttpMistralClient(new HttpClient(handler), Settings());

        await Assert.ThrowsAsync<MistralException>(() => client.CompleteJsonAsync("m", "s", "u", CancellationToken.None));
    }

    [Test]
    public async Task Timeout_while_caller_token_is_not_cancelled_throws_MistralException() {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueException(new TaskCanceledException("timeout"));
        var client = new HttpMistralClient(new HttpClient(handler), Settings());

        await Assert.ThrowsAsync<MistralException>(() => client.CompleteJsonAsync("m", "s", "u", CancellationToken.None));
    }

    [Test]
    public async Task Non_success_status_throws_MistralException_with_status() {
        var handler = new StubHttpMessageHandler();
        for (var i = 0; i < 3; i++)
            handler.Enqueue(HttpStatusCode.TooManyRequests, """{"message":"rate limited"}""");
        var client = new HttpMistralClient(new HttpClient(handler), Settings());

        var ex = await Assert.ThrowsAsync<MistralException>(() => client.CompleteJsonAsync("m", "s", "u", CancellationToken.None));

        await Assert.That(ex!.Message).Contains("429");
    }
}
