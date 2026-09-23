using System.Net;
using Quartermaster.Server.NewsPosts;
using Quartermaster.Server.NewsPosts.Postiz;
using Quartermaster.Server.Tests.NewsPosts.Fakes;

namespace Quartermaster.Server.Tests.NewsPosts;

public class HttpPostizClientTests {
    private static NewsPostsSettings Settings() => new() { PostizBaseUrl = "https://postiz.test/api", PostizApiKey = "pk" };

    [Test]
    public async Task GetChannelsAsync_maps_integrations_and_sends_raw_key() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, """
            [{"id":"a","name":"Piraten X","identifier":"x","picture":"p","disabled":false,"profile":"piraten","customer":null},
             {"id":"b","name":"Alt","identifier":"bluesky","picture":null,"disabled":true,"profile":"alt"}]
            """);
        var client = new HttpPostizClient(new HttpClient(handler), Settings());

        var channels = await client.GetChannelsAsync(CancellationToken.None);

        await Assert.That(channels.Count).IsEqualTo(2);
        await Assert.That(channels[1].Disabled).IsTrue();
        await Assert.That(handler.Requests[0].RequestUri!.ToString()).IsEqualTo("https://postiz.test/api/public/v1/integrations");
        await Assert.That(handler.Requests[0].Headers.GetValues("Authorization").Single()).IsEqualTo("pk");
    }

    [Test]
    public async Task CreatePostAsync_returns_post_ids() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.Created, """[{"postId":"p1","integration":"a"},{"postId":"p2","integration":"b"}]""");
        var client = new HttpPostizClient(new HttpClient(handler), Settings());
        var channels = new List<PostizChannel> {
            new("a", "X", "x", null, null, false),
            new("b", "M", "mastodon", null, null, false)
        };

        var ids = await client.CreatePostAsync("Text", channels, null, CancellationToken.None);

        await Assert.That(ids).IsEquivalentTo(new List<string> { "p1", "p2" });
        await Assert.That(handler.Requests[0].RequestUri!.ToString()).IsEqualTo("https://postiz.test/api/public/v1/posts");
    }

    [Test]
    public async Task CreatePostAsync_throws_PostizException_on_error() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.BadRequest, """{"message":"invalid"}""");
        var client = new HttpPostizClient(new HttpClient(handler), Settings());

        var ex = await Assert.ThrowsAsync<PostizException>(() =>
            client.CreatePostAsync("Text", [new PostizChannel("a", "X", "x", null, null, false)], null, CancellationToken.None));

        await Assert.That(ex!.Message).Contains("400");
    }

    [Test]
    public async Task CreatePostAsync_with_unexpected_success_body_returns_empty_list() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.Created, """{"ok":true}""");
        var client = new HttpPostizClient(new HttpClient(handler), Settings());

        var ids = await client.CreatePostAsync("Text", [new PostizChannel("a", "X", "x", null, null, false)], null, CancellationToken.None);

        await Assert.That(ids).IsEmpty();
    }

    [Test]
    [Arguments("")]
    [Arguments("<html><body>OK</body></html>")]
    public async Task CreatePostAsync_with_non_json_success_body_returns_empty_list(string body) {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.Created, body, "text/html");
        var client = new HttpPostizClient(new HttpClient(handler), Settings());

        var ids = await client.CreatePostAsync("Text", [new PostizChannel("a", "X", "x", null, null, false)], null, CancellationToken.None);

        await Assert.That(ids).IsEmpty();
    }

    [Test]
    public async Task CreatePostAsync_with_body_read_timeout_after_success_returns_empty_list() {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueFailingBody(HttpStatusCode.Created, new TaskCanceledException("body read timed out"));
        var client = new HttpPostizClient(new HttpClient(handler), Settings());

        var ids = await client.CreatePostAsync("Text", [new PostizChannel("a", "X", "x", null, null, false)], null, CancellationToken.None);

        await Assert.That(ids).IsEmpty();
    }

    [Test]
    public async Task GetChannelsAsync_with_non_json_body_throws_PostizException() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, "<html>login</html>", "text/html");
        var client = new HttpPostizClient(new HttpClient(handler), Settings());

        await Assert.ThrowsAsync<PostizException>(() => client.GetChannelsAsync(CancellationToken.None));
    }

    [Test]
    public async Task GetChannelsAsync_with_unexpected_shape_throws_PostizException() {
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, """{"not":"an array"}""");
        var client = new HttpPostizClient(new HttpClient(handler), Settings());

        await Assert.ThrowsAsync<PostizException>(() => client.GetChannelsAsync(CancellationToken.None));
    }

    [Test]
    public async Task Timeout_while_caller_token_is_not_cancelled_throws_PostizException() {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueException(new TaskCanceledException("timeout"));
        var client = new HttpPostizClient(new HttpClient(handler), Settings());

        await Assert.ThrowsAsync<PostizException>(() => client.GetChannelsAsync(CancellationToken.None));
    }
}
