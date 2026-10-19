using System.Net;
using System.Text.Json;
using Quartermaster.Server.AssemblyMotions.Forum;
using Quartermaster.Server.Tests.AssemblyMotions.Fakes;

namespace Quartermaster.Server.Tests.AssemblyMotions;

public class HttpDiscourseClientTests {
    private const string Key = "very-secret-api-key";

    private static DiscourseSettings Settings(string url = "https://forum.invalid")
        => new(url, "antragsbot", Key, 32000);

    private static HttpDiscourseClient Client(RecordingDiscourseHandler handler, CapturingLogger<HttpDiscourseClient>? logger = null)
        => new(new HttpClient(handler), logger ?? new CapturingLogger<HttpDiscourseClient>());

    private static async Task<DiscourseException> Capture(Func<Task> action) {
        try {
            await action();
        } catch (DiscourseException ex) {
            return ex;
        }

        throw new InvalidOperationException("No DiscourseException thrown.");
    }

    [Test]
    public async Task Sends_api_key_and_username_headers() {
        var handler = new RecordingDiscourseHandler();
        handler.EnqueueOk("""{"current_user":{"id":3,"username":"antragsbot","moderator":true,"admin":false}}""");
        using var client = Client(handler);

        var user = await client.GetCurrentUserAsync(Settings(), CancellationToken.None);

        var request = handler.Requests.Single();
        await Assert.That(request.Url).IsEqualTo("https://forum.invalid/session/current.json");
        await Assert.That(request.Headers["Api-Key"]).IsEqualTo(Key);
        await Assert.That(request.Headers["Api-Username"]).IsEqualTo("antragsbot");
        await Assert.That(user.Username).IsEqualTo("antragsbot");
        await Assert.That(user.IsStaff).IsTrue();
    }

    [Test]
    public async Task Create_topic_posts_title_raw_category_and_external_id() {
        var handler = new RecordingDiscourseHandler();
        handler.EnqueueOk("""{"id":501,"topic_id":77,"topic_slug":"pa001"}""");
        using var client = Client(handler);

        var topic = await client.CreateTopicAsync(Settings(), "PA001: Titel", "Text", 7, "qm-motion-1", CancellationToken.None);

        var request = handler.Requests.Single();
        await Assert.That(request.Method).IsEqualTo(HttpMethod.Post);
        await Assert.That(request.Url).IsEqualTo("https://forum.invalid/posts.json");
        using var body = JsonDocument.Parse(request.Body);
        await Assert.That(body.RootElement.GetProperty("title").GetString()).IsEqualTo("PA001: Titel");
        await Assert.That(body.RootElement.GetProperty("raw").GetString()).IsEqualTo("Text");
        await Assert.That(body.RootElement.GetProperty("category").GetInt32()).IsEqualTo(7);
        await Assert.That(body.RootElement.GetProperty("external_id").GetString()).IsEqualTo("qm-motion-1");
        await Assert.That(topic.TopicId).IsEqualTo(77);
        await Assert.That(topic.FirstPostId).IsEqualTo(501);
    }

    [Test]
    public async Task Edit_post_wraps_raw_and_edit_reason_in_post_object() {
        var handler = new RecordingDiscourseHandler();
        handler.EnqueueOk("""{"post":{"id":501}}""");
        using var client = Client(handler);

        await client.EditPostAsync(Settings(), 501, "Neu", "Antragsportal: Revision 2 eingereicht", CancellationToken.None);

        var request = handler.Requests.Single();
        await Assert.That(request.Method).IsEqualTo(HttpMethod.Put);
        await Assert.That(request.Url).IsEqualTo("https://forum.invalid/posts/501.json");
        using var body = JsonDocument.Parse(request.Body);
        var post = body.RootElement.GetProperty("post");
        await Assert.That(post.GetProperty("raw").GetString()).IsEqualTo("Neu");
        await Assert.That(post.GetProperty("edit_reason").GetString()).IsEqualTo("Antragsportal: Revision 2 eingereicht");
    }

    [Test]
    public async Task Update_topic_title_puts_title() {
        var handler = new RecordingDiscourseHandler();
        handler.EnqueueOk("""{"basic_topic":{"id":77}}""");
        using var client = Client(handler);

        await client.UpdateTopicTitleAsync(Settings(), 77, "PA001: Neu", CancellationToken.None);

        var request = handler.Requests.Single();
        await Assert.That(request.Method).IsEqualTo(HttpMethod.Put);
        await Assert.That(request.Url).IsEqualTo("https://forum.invalid/t/-/77.json");
        using var body = JsonDocument.Parse(request.Body);
        await Assert.That(body.RootElement.GetProperty("title").GetString()).IsEqualTo("PA001: Neu");
    }

    [Test]
    public async Task Find_by_external_id_follows_one_redirect() {
        var handler = new RecordingDiscourseHandler();
        handler.EnqueueRedirect("https://forum.invalid/t/pa001/77.json");
        handler.EnqueueOk("""{"id":77,"title":"PA001: Titel","deleted_at":null,"post_stream":{"posts":[{"id":501}]}}""");
        using var client = Client(handler);

        var topic = await client.FindTopicByExternalIdAsync(Settings(), "qm-motion-1", CancellationToken.None);

        await Assert.That(handler.Requests[0].Url).IsEqualTo("https://forum.invalid/t/external_id/qm-motion-1.json");
        await Assert.That(handler.Requests[1].Url).IsEqualTo("https://forum.invalid/t/pa001/77.json");
        await Assert.That(topic!.TopicId).IsEqualTo(77);
        await Assert.That(topic.FirstPostId).IsEqualTo(501);
    }

    [Test]
    public async Task Find_by_external_id_returns_null_on_404() {
        var handler = new RecordingDiscourseHandler();
        handler.Enqueue(HttpStatusCode.NotFound, """{"errors":["Nicht gefunden"],"error_type":"not_found"}""");
        using var client = Client(handler);

        var topic = await client.FindTopicByExternalIdAsync(Settings(), "qm-motion-1", CancellationToken.None);

        await Assert.That(topic).IsNull();
    }

    [Test]
    public async Task Find_by_external_id_refuses_foreign_redirect_host() {
        var handler = new RecordingDiscourseHandler();
        handler.EnqueueRedirect("https://evil.invalid/t/pa001/77.json");
        using var client = Client(handler);

        var ex = await Capture(() => client.FindTopicByExternalIdAsync(Settings(), "qm-motion-1", CancellationToken.None));

        await Assert.That(ex.Code).IsEqualTo(DiscourseException.InvalidResponse);
        await Assert.That(handler.Requests.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Topic_deleted_at_is_read() {
        var handler = new RecordingDiscourseHandler();
        handler.EnqueueOk("""{"id":77,"title":"PA001: Titel","deleted_at":"2026-10-19T10:00:00.000Z","post_stream":{"posts":[{"id":501}]}}""");
        using var client = Client(handler);

        var topic = await client.GetTopicAsync(Settings(), 77, CancellationToken.None);

        await Assert.That(topic.Deleted).IsTrue();
    }

    [Test]
    public async Task Post_raw_and_deleted_at_are_read() {
        var handler = new RecordingDiscourseHandler();
        handler.EnqueueOk("""{"id":501,"raw":"Text","deleted_at":null}""");
        using var client = Client(handler);

        var post = await client.GetPostAsync(Settings(), 501, CancellationToken.None);

        await Assert.That(post.Raw).IsEqualTo("Text");
        await Assert.That(post.Deleted).IsFalse();
    }

    [Test]
    public async Task Category_permission_and_read_restriction_are_read() {
        var handler = new RecordingDiscourseHandler();
        handler.EnqueueOk("""{"category":{"id":7,"name":"Anträge","read_restricted":true,"permission":1}}""");
        using var client = Client(handler);

        var category = await client.GetCategoryAsync(Settings(), 7, CancellationToken.None);

        await Assert.That(handler.Requests.Single().Url).IsEqualTo("https://forum.invalid/c/7/show.json");
        await Assert.That(category.Name).IsEqualTo("Anträge");
        await Assert.That(category.ReadRestricted).IsTrue();
        await Assert.That(category.CanCreateTopic).IsTrue();
    }

    [Test]
    [Arguments(401, "unauthorized")]
    [Arguments(403, "unauthorized")]
    [Arguments(404, "not_found")]
    [Arguments(500, "http_error")]
    public async Task Status_codes_are_translated(int status, string code) {
        var handler = new RecordingDiscourseHandler();
        handler.Enqueue((HttpStatusCode)status, """{"errors":["x"]}""");
        using var client = Client(handler);

        var ex = await Capture(() => client.GetPostAsync(Settings(), 501, CancellationToken.None));

        await Assert.That(ex.Code).IsEqualTo(code);
        await Assert.That(ex.StatusCode).IsEqualTo(status);
    }

    [Test]
    public async Task Rejected_post_carries_discourse_errors() {
        var handler = new RecordingDiscourseHandler();
        handler.Enqueue((HttpStatusCode)422, """{"action":"create_post","errors":["Der Beitrag ist zu lang.","Noch etwas."]}""");
        using var client = Client(handler);

        var ex = await Capture(() => client.CreateTopicAsync(Settings(), "PA001: Titel", "Text", 7, "qm-motion-1", CancellationToken.None));

        await Assert.That(ex.Code).IsEqualTo(DiscourseException.Rejected);
        await Assert.That(ex.Message).Contains("Der Beitrag ist zu lang.");
        await Assert.That(ex.Message).Contains("Noch etwas.");
    }

    [Test]
    public async Task Rate_limit_carries_retry_after() {
        var handler = new RecordingDiscourseHandler();
        handler.Enqueue((HttpStatusCode)429, """{"errors":["slow down"]}""", new Dictionary<string, string> { ["Retry-After"] = "42" });
        using var client = Client(handler);

        var ex = await Capture(() => client.GetPostAsync(Settings(), 501, CancellationToken.None));

        await Assert.That(ex.Code).IsEqualTo(DiscourseException.RateLimited);
        await Assert.That(ex.RetryAfter).IsEqualTo(TimeSpan.FromSeconds(42));
    }

    [Test]
    public async Task Insecure_base_url_sends_nothing() {
        var handler = new RecordingDiscourseHandler();
        using var client = Client(handler);

        var ex = await Capture(() => client.GetCurrentUserAsync(Settings("http://forum.invalid"), CancellationToken.None));

        await Assert.That(ex.Code).IsEqualTo(DiscourseException.InsecureUrl);
        await Assert.That(handler.Requests.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Key_never_appears_in_logs() {
        var handler = new RecordingDiscourseHandler();
        handler.Enqueue(HttpStatusCode.Forbidden, $$"""{"errors":["invalid {{Key}}"],"error_type":"invalid_access"}""");
        var logger = new CapturingLogger<HttpDiscourseClient>();
        using var client = Client(handler, logger);

        await Capture(() => client.GetCurrentUserAsync(Settings(), CancellationToken.None));

        await Assert.That(logger.Entries.Any(e => e.Contains(Key))).IsFalse();
    }
}
