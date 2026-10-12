using System.Net;
using Quartermaster.Server.AssemblyMotions.Wiki;
using Quartermaster.Server.Tests.AssemblyMotions.Fakes;

namespace Quartermaster.Server.Tests.AssemblyMotions;

public class HttpWikiBotClientTests {
    private const string Password = "hunter2-secret";
    private const string EditOk = """{"edit":{"result":"Success","pageid":5,"title":"T"}}""";

    private static WikiBotSettings Settings(string user = "Bot@Quartermaster", string apiUrl = "https://wiki.test/api.php")
        => new(apiUrl, user, Password, "Antrag");

    private static HttpWikiBotClient Client(RecordingWikiHandler handler, CapturingLogger<HttpWikiBotClient>? logger = null)
        => new(handler, logger ?? new CapturingLogger<HttpWikiBotClient>());

    [Test]
    public async Task Login_runs_token_login_csrf_sequence() {
        var handler = new RecordingWikiHandler();
        handler.EnqueueLogin();
        using var client = Client(handler);

        await client.LoginAsync(Settings(), CancellationToken.None);

        var requests = handler.Requests;
        await Assert.That(requests.Count).IsEqualTo(3);
        await Assert.That(requests[0].Method).IsEqualTo(HttpMethod.Get);
        await Assert.That(requests[0].Fields["type"]).IsEqualTo("login");
        await Assert.That(requests[1].Method).IsEqualTo(HttpMethod.Post);
        await Assert.That(requests[1].Fields["lgname"]).IsEqualTo("Bot@Quartermaster");
        await Assert.That(requests[1].Fields["lgpassword"]).IsEqualTo(Password);
        await Assert.That(requests[1].Fields["lgtoken"]).IsEqualTo("lt+\\");
        await Assert.That(requests[2].Method).IsEqualTo(HttpMethod.Get);
        await Assert.That(requests[2].Fields.ContainsKey("type")).IsFalse();
        await Assert.That(requests.All(r => r.Fields["format"] == "json" && r.Fields["formatversion"] == "2")).IsTrue();
        await Assert.That(requests[1].Url).DoesNotContain("lgpassword");
    }

    [Test]
    public async Task Every_request_sends_user_agent() {
        var handler = new RecordingWikiHandler();
        handler.EnqueueLogin();
        handler.Enqueue("query", """{"query":{"pages":[{"title":"A","missing":true}]}}""");
        using var client = Client(handler);

        await client.GetContentAsync(Settings(), "A", CancellationToken.None);

        await Assert.That(handler.Requests.Count).IsEqualTo(4);
        await Assert.That(handler.Requests.All(r => r.Headers["User-Agent"] == "Quartermaster/4 (+https://piratenpartei.de)")).IsTrue();
    }

    [Test]
    public async Task Missing_page_returns_null() {
        var handler = new RecordingWikiHandler();
        handler.EnqueueLogin();
        handler.Enqueue("query", """{"query":{"pages":[{"title":"A","missing":true}]}}""");
        using var client = Client(handler);

        var content = await client.GetContentAsync(Settings(), "A", CancellationToken.None);

        await Assert.That(content).IsNull();
    }

    [Test]
    public async Task Existing_page_returns_main_slot_content() {
        var handler = new RecordingWikiHandler();
        handler.EnqueueLogin();
        handler.Enqueue("query", """{"query":{"pages":[{"title":"A","revisions":[{"slots":{"main":{"content":"Hallo {{Vorlage}}"}}}]}]}}""");
        using var client = Client(handler);

        var content = await client.GetContentAsync(Settings(), "A", CancellationToken.None);

        await Assert.That(content).IsEqualTo("Hallo {{Vorlage}}");
        var request = handler.Requests.Last();
        await Assert.That(request.Fields["prop"]).IsEqualTo("revisions");
        await Assert.That(request.Fields["rvslots"]).IsEqualTo("main");
        await Assert.That(request.Fields["titles"]).IsEqualTo("A");
    }

    [Test]
    public async Task Edit_sends_bot_and_assert_user() {
        var handler = new RecordingWikiHandler();
        handler.EnqueueLogin();
        handler.Enqueue("edit", EditOk);
        using var client = Client(handler);

        await client.EditAsync(Settings(), "Seite", "Text", "Zusammenfassung", CancellationToken.None);

        var edit = handler.Requests.Last();
        await Assert.That(edit.Method).IsEqualTo(HttpMethod.Post);
        await Assert.That(edit.Fields["action"]).IsEqualTo("edit");
        await Assert.That(edit.Fields["title"]).IsEqualTo("Seite");
        await Assert.That(edit.Fields["text"]).IsEqualTo("Text");
        await Assert.That(edit.Fields["summary"]).IsEqualTo("Zusammenfassung");
        await Assert.That(edit.Fields["bot"]).IsEqualTo("1");
        await Assert.That(edit.Fields["assert"]).IsEqualTo("user");
        await Assert.That(edit.Fields["token"]).IsEqualTo("csrf+\\");
        await Assert.That(edit.Fields.ContainsKey("nocreate")).IsFalse();
        await Assert.That(edit.RawBody).Contains("token=csrf%2B%5C");
        await Assert.That(edit.RawBody).DoesNotContain("+");
    }

    [Test]
    public async Task Edit_nochange_is_success() {
        var handler = new RecordingWikiHandler();
        handler.EnqueueLogin();
        handler.Enqueue("edit", """{"edit":{"result":"Success","nochange":true,"title":"T"}}""");
        using var client = Client(handler);

        await client.EditAsync(Settings(), "Seite", "Text", "s", CancellationToken.None);
    }

    [Test]
    public async Task Edit_failure_with_abusefilter_throws() {
        var handler = new RecordingWikiHandler();
        handler.EnqueueLogin();
        handler.Enqueue("edit", """{"edit":{"result":"Failure","abusefilter":{"id":3,"description":"Keine Links"}}}""");
        using var client = Client(handler);

        var ex = await Assert.ThrowsAsync<WikiBotException>(() => client.EditAsync(Settings(), "Seite", "Text", "s", CancellationToken.None));

        await Assert.That(ex!.Code).IsEqualTo("abusefilter");
        await Assert.That(ex.Message).Contains("Keine Links");
    }

    [Test]
    public async Task Edit_failure_with_captcha_throws() {
        var handler = new RecordingWikiHandler();
        handler.EnqueueLogin();
        handler.Enqueue("edit", """{"edit":{"result":"Failure","captcha":{"type":"simple","mime":"text/plain","id":"1","question":"1+1"}}}""");
        using var client = Client(handler);

        var ex = await Assert.ThrowsAsync<WikiBotException>(() => client.EditAsync(Settings(), "Seite", "Text", "s", CancellationToken.None));

        await Assert.That(ex!.Code).IsEqualTo("captcha");
        await Assert.That(ex.Message).Contains("Captcha verlangt");
    }

    [Test]
    public async Task Protect_sends_level_for_edit_and_move() {
        var handler = new RecordingWikiHandler();
        handler.EnqueueLogin();
        handler.Enqueue("protect", """{"protect":{"title":"Seite","reason":"","protections":[{"edit":"Antrag","expiry":"infinite"}]}}""");
        using var client = Client(handler);

        await client.ProtectAsync(Settings(), "Seite", CancellationToken.None);

        var protect = handler.Requests.Last();
        await Assert.That(protect.Fields["action"]).IsEqualTo("protect");
        await Assert.That(protect.Fields["protections"]).IsEqualTo("edit=Antrag|move=Antrag");
        await Assert.That(protect.Fields["expiry"]).IsEqualTo("infinite|infinite");
        await Assert.That(protect.Fields["assert"]).IsEqualTo("user");
        await Assert.That(protect.Fields["token"]).IsEqualTo("csrf+\\");
    }

    [Test]
    public async Task Badtoken_triggers_one_relogin_and_retry() {
        var handler = new RecordingWikiHandler();
        handler.EnqueueLogin();
        handler.Enqueue("edit", """{"error":{"code":"badtoken","info":"Invalid CSRF token."}}""");
        handler.EnqueueLogin("lt2+\\", "csrf2+\\");
        handler.Enqueue("edit", EditOk);
        using var client = Client(handler);

        await client.EditAsync(Settings(), "Seite", "Text", "s", CancellationToken.None);

        await Assert.That(string.Join(",", handler.Actions)).IsEqualTo("query,login,query,edit,query,login,query,edit");
    }

    [Test]
    public async Task Relogin_after_assertuserfailed_uses_fresh_token() {
        var handler = new RecordingWikiHandler();
        handler.EnqueueLogin();
        handler.Enqueue("edit", """{"error":{"code":"assertuserfailed","info":"Assertion that the user is logged in failed."}}""");
        handler.EnqueueLogin("lt2+\\", "csrf2+\\");
        handler.Enqueue("edit", EditOk);
        using var client = Client(handler);

        await client.EditAsync(Settings(), "Seite", "Text", "s", CancellationToken.None);

        var edits = handler.Requests.Where(r => r.Action == "edit").ToList();
        await Assert.That(edits.Count).IsEqualTo(2);
        await Assert.That(edits[0].Fields["token"]).IsEqualTo("csrf+\\");
        await Assert.That(edits[1].Fields["token"]).IsEqualTo("csrf2+\\");
    }

    [Test]
    public async Task Second_failure_throws() {
        var handler = new RecordingWikiHandler();
        handler.EnqueueLogin();
        handler.Enqueue("edit", """{"error":{"code":"badtoken","info":"Invalid CSRF token."}}""");
        handler.EnqueueLogin();
        handler.Enqueue("edit", """{"error":{"code":"badtoken","info":"Invalid CSRF token."}}""");
        using var client = Client(handler);

        var ex = await Assert.ThrowsAsync<WikiBotException>(() => client.EditAsync(Settings(), "Seite", "Text", "s", CancellationToken.None));

        await Assert.That(ex!.Code).IsEqualTo("badtoken");
        await Assert.That(handler.Requests.Count(r => r.Action == "edit")).IsEqualTo(2);
    }

    [Test]
    public async Task Html_response_throws_blocked() {
        var handler = new RecordingWikiHandler();
        handler.Enqueue("tokens-login", "<!doctype html><html>Anubis</html>");
        using var client = Client(handler);

        var ex = await Assert.ThrowsAsync<WikiBotException>(() => client.LoginAsync(Settings(), CancellationToken.None));

        await Assert.That(ex!.Code).IsEqualTo("blocked_html");
        await Assert.That(ex.Message).Contains("HTML statt JSON");
    }

    [Test]
    public async Task Changed_settings_force_new_login() {
        var handler = new RecordingWikiHandler();
        handler.EnqueueLogin();
        handler.Enqueue("edit", EditOk);
        handler.Enqueue("edit", EditOk);
        handler.EnqueueLogin("lt2+\\", "csrf2+\\");
        handler.Enqueue("edit", EditOk);
        using var client = Client(handler);

        await client.EditAsync(Settings(), "A", "t", "s", CancellationToken.None);
        await client.EditAsync(Settings(), "B", "t", "s", CancellationToken.None);
        await client.EditAsync(Settings("Other@Bot"), "C", "t", "s", CancellationToken.None);

        await Assert.That(handler.Requests.Count(r => r.Action == "login")).IsEqualTo(2);
        await Assert.That(handler.Requests.Last().Fields["token"]).IsEqualTo("csrf2+\\");
    }

    [Test]
    public async Task Login_failure_throws_with_reason() {
        var handler = new RecordingWikiHandler();
        handler.Enqueue("tokens-login", """{"query":{"tokens":{"logintoken":"lt+\\"}}}""");
        handler.Enqueue("login", """{"login":{"result":"Failed","reason":"Falsches Passwort."}}""");
        using var client = Client(handler);

        var ex = await Assert.ThrowsAsync<WikiBotException>(() => client.LoginAsync(Settings(), CancellationToken.None));

        await Assert.That(ex!.Code).IsEqualTo("login_failed");
        await Assert.That(ex.Message).Contains("Falsches Passwort.");
    }

    [Test]
    public async Task Login_failure_without_reason_names_the_result() {
        var handler = new RecordingWikiHandler();
        handler.Enqueue("tokens-login", """{"query":{"tokens":{"logintoken":"lt+\\"}}}""");
        handler.Enqueue("login", """{"login":{"result":"Throttled"}}""");
        using var client = Client(handler);

        var ex = await Assert.ThrowsAsync<WikiBotException>(() => client.LoginAsync(Settings(), CancellationToken.None));

        await Assert.That(ex!.Message).Contains("Throttled");
    }

    [Test]
    public async Task Error_code_is_exposed() {
        var handler = new RecordingWikiHandler();
        handler.EnqueueLogin();
        handler.Enqueue("edit", """{"error":{"code":"readonly","info":"The wiki is currently in read-only mode."}}""");
        using var client = Client(handler);

        var ex = await Assert.ThrowsAsync<WikiBotException>(() => client.EditAsync(Settings(), "Seite", "Text", "s", CancellationToken.None));

        await Assert.That(ex!.Code).IsEqualTo("readonly");
        await Assert.That(ex.Message).Contains("read-only");
        await Assert.That(handler.Requests.Count(r => r.Action == "login")).IsEqualTo(1);
    }

    [Test]
    public async Task Http_500_throws_http_error() {
        var handler = new RecordingWikiHandler();
        handler.EnqueueLogin();
        handler.Enqueue("edit", new string('x', 5000), HttpStatusCode.InternalServerError);
        using var client = Client(handler);

        var ex = await Assert.ThrowsAsync<WikiBotException>(() => client.EditAsync(Settings(), "Seite", "Text", "s", CancellationToken.None));

        await Assert.That(ex!.Code).IsEqualTo("http_error");
        await Assert.That(ex.Message).Contains("500");
        await Assert.That(ex.Message.Length).IsLessThan(400);
    }

    [Test]
    public async Task Large_text_is_sent_complete() {
        var handler = new RecordingWikiHandler();
        handler.EnqueueLogin();
        handler.Enqueue("edit", EditOk);
        using var client = Client(handler);
        var text = string.Concat(Enumerable.Repeat("Äpfel & Birnen = 100% + mehr | {{x}}\n", 8500));

        await client.EditAsync(Settings(), "Seite", text, "s", CancellationToken.None);

        await Assert.That(text.Length).IsGreaterThan(300_000);
        await Assert.That(handler.Requests.Last().Fields["text"]).IsEqualTo(text);
    }

    [Test]
    public async Task Title_with_umlaut_colon_slash_is_encoded() {
        var handler = new RecordingWikiHandler();
        handler.EnqueueLogin();
        handler.Enqueue("query", """{"query":{"pages":[{"title":"x","missing":true}]}}""");
        handler.Enqueue("edit", EditOk);
        using var client = Client(handler);
        const string title = "BPT 2026.1:Anträge/Ä Test";

        await client.GetContentAsync(Settings(), title, CancellationToken.None);
        await client.EditAsync(Settings(), title, "t", "s", CancellationToken.None);

        var requests = handler.Requests;
        var read = requests.First(r => r.Action == "query" && r.Fields.ContainsKey("titles"));
        await Assert.That(read.Fields["titles"]).IsEqualTo(title);
        await Assert.That(read.Url).Contains("titles=BPT%202026.1%3AAntr%C3%A4ge%2F%C3%84%20Test");
        await Assert.That(requests.Last().Fields["title"]).IsEqualTo(title);
    }

    [Test]
    public async Task Password_never_appears_in_exception_or_log() {
        var handler = new RecordingWikiHandler();
        handler.Enqueue("tokens-login", """{"query":{"tokens":{"logintoken":"lt+\\"}}}""");
        handler.Enqueue("login", """{"login":{"result":"Failed","reason":"Nope"}}""");
        handler.EnqueueLogin();
        handler.Enqueue("edit", """{"error":{"code":"permissiondenied","info":"No."}}""");
        var logger = new CapturingLogger<HttpWikiBotClient>();
        using var client = Client(handler, logger);

        var loginEx = await Assert.ThrowsAsync<WikiBotException>(() => client.LoginAsync(Settings(), CancellationToken.None));
        var editEx = await Assert.ThrowsAsync<WikiBotException>(() => client.EditAsync(Settings(), "Seite", "Text", "s", CancellationToken.None));

        await Assert.That(logger.Entries.Count).IsGreaterThan(0);
        var everything = string.Join("\n", logger.Entries) + loginEx + editEx + Settings();
        await Assert.That(everything).DoesNotContain(Password);
        await Assert.That(everything).DoesNotContain("csrf+");
        await Assert.That(everything).DoesNotContain("lt+");
    }

    [Test]
    public async Task Concurrent_calls_login_once() {
        var handler = new RecordingWikiHandler { Delay = TimeSpan.FromMilliseconds(20) };
        handler.EnqueueLogin();
        handler.Enqueue("query", """{"query":{"pages":[{"title":"A","missing":true}]}}""");
        handler.Enqueue("query", """{"query":{"pages":[{"title":"B","missing":true}]}}""");
        using var client = Client(handler);

        await Task.WhenAll(
            client.GetContentAsync(Settings(), "A", CancellationToken.None),
            client.GetContentAsync(Settings(), "B", CancellationToken.None));

        await Assert.That(handler.Requests.Count(r => r.Action == "login")).IsEqualTo(1);
        await Assert.That(handler.Requests.Count).IsEqualTo(5);
    }

    [Test]
    public async Task Unconfigured_settings_throw_without_request() {
        var handler = new RecordingWikiHandler();
        using var client = Client(handler);

        var ex = await Assert.ThrowsAsync<WikiBotException>(() => client.LoginAsync(new WikiBotSettings("https://wiki.test/api.php", "", "", "Antrag"), CancellationToken.None));

        await Assert.That(ex!.Code).IsEqualTo("login_failed");
        await Assert.That(handler.Requests.Count).IsEqualTo(0);
    }

    private static async Task AssertUnexpectedRead(string pageJson) {
        var handler = new RecordingWikiHandler();
        handler.EnqueueLogin();
        handler.Enqueue("query", pageJson);
        using var client = Client(handler);

        var ex = await Assert.ThrowsAsync<WikiBotException>(() => client.GetContentAsync(Settings(), "A", CancellationToken.None));

        await Assert.That(ex!.Code).IsEqualTo("unexpected_response");
    }

    [Test]
    public async Task Hidden_revision_text_throws_unexpected_response()
        => await AssertUnexpectedRead("""{"query":{"pages":[{"title":"A","revisions":[{"slots":{"main":{"texthidden":true}}}]}]}}""");

    [Test]
    public async Task Page_without_revisions_throws_unexpected_response()
        => await AssertUnexpectedRead("""{"query":{"pages":[{"title":"A"}]}}""");

    [Test]
    public async Task Non_string_content_throws_unexpected_response()
        => await AssertUnexpectedRead("""{"query":{"pages":[{"title":"A","revisions":[{"slots":{"main":{"content":5}}}]}]}}""");

    [Test]
    public async Task Response_without_query_throws_unexpected_response()
        => await AssertUnexpectedRead("""{"batchcomplete":true}""");

    [Test]
    public async Task Array_root_throws_unexpected_response()
        => await AssertUnexpectedRead("[]");

    [Test]
    public async Task Null_root_throws_unexpected_response()
        => await AssertUnexpectedRead("null");

    [Test]
    public async Task Non_object_error_throws_unexpected_response()
        => await AssertUnexpectedRead("""{"error":"boom"}""");

    [Test]
    public async Task Non_string_token_throws_unexpected_response() {
        var handler = new RecordingWikiHandler();
        handler.Enqueue("tokens-login", """{"query":{"tokens":{"logintoken":7}}}""");
        using var client = Client(handler);

        var ex = await Assert.ThrowsAsync<WikiBotException>(() => client.LoginAsync(Settings(), CancellationToken.None));

        await Assert.That(ex!.Code).IsEqualTo("unexpected_response");
    }

    [Test]
    public async Task Edit_without_edit_property_throws_unexpected_response() {
        var handler = new RecordingWikiHandler();
        handler.EnqueueLogin();
        handler.Enqueue("edit", "{}");
        using var client = Client(handler);

        var ex = await Assert.ThrowsAsync<WikiBotException>(() => client.EditAsync(Settings(), "Seite", "t", "s", CancellationToken.None));

        await Assert.That(ex!.Code).IsEqualTo("unexpected_response");
    }

    [Test]
    public async Task Protect_without_protect_property_throws_unexpected_response() {
        var handler = new RecordingWikiHandler();
        handler.EnqueueLogin();
        handler.Enqueue("protect", "{}");
        using var client = Client(handler);

        var ex = await Assert.ThrowsAsync<WikiBotException>(() => client.ProtectAsync(Settings(), "Seite", CancellationToken.None));

        await Assert.That(ex!.Code).IsEqualTo("unexpected_response");
    }

    [Test]
    public async Task Lone_surrogates_are_replaced_before_encoding() {
        var handler = new RecordingWikiHandler();
        handler.EnqueueLogin();
        handler.Enqueue("edit", EditOk);
        using var client = Client(handler);

        await client.EditAsync(Settings(), "Seite\uD800", "a\uDC00b\uD83D\uDE00c", "s\uD800", CancellationToken.None);

        var edit = handler.Requests.Last();
        await Assert.That(edit.Fields["title"]).IsEqualTo("Seite\uFFFD");
        await Assert.That(edit.Fields["text"]).IsEqualTo("a\uFFFDb\uD83D\uDE00c");
        await Assert.That(edit.Fields["summary"]).IsEqualTo("s\uFFFD");
    }

    [Test]
    [Arguments("http://wiki.test/api.php")]
    [Arguments("ftp://wiki.test/api.php")]
    [Arguments("not a url")]
    public async Task Insecure_url_throws_before_any_request(string apiUrl) {
        var handler = new RecordingWikiHandler();
        using var client = Client(handler);

        var ex = await Assert.ThrowsAsync<WikiBotException>(() => client.GetContentAsync(Settings(apiUrl: apiUrl), "A", CancellationToken.None));

        await Assert.That(ex!.Code).IsEqualTo("insecure_url");
        await Assert.That(handler.Requests.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments("http://localhost:8080/api.php")]
    [Arguments("http://127.0.0.1/api.php")]
    [Arguments("http://[::1]/api.php")]
    public async Task Http_is_allowed_for_loopback(string apiUrl) {
        var handler = new RecordingWikiHandler();
        handler.EnqueueLogin();
        using var client = Client(handler);

        await client.LoginAsync(Settings(apiUrl: apiUrl), CancellationToken.None);

        await Assert.That(handler.Requests.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Login_and_read_failures_are_logged_as_warning_without_secrets() {
        var handler = new RecordingWikiHandler();
        handler.Enqueue("tokens-login", """{"query":{"tokens":{"logintoken":"lt+\\"}}}""");
        handler.Enqueue("login", """{"login":{"result":"Failed","reason":"Nope"}}""");
        handler.EnqueueLogin();
        handler.Enqueue("query", "[]");
        var logger = new CapturingLogger<HttpWikiBotClient>();
        using var client = Client(handler, logger);

        await Assert.ThrowsAsync<WikiBotException>(() => client.LoginAsync(Settings(), CancellationToken.None));
        await Assert.ThrowsAsync<WikiBotException>(() => client.GetContentAsync(Settings(), "A", CancellationToken.None));

        await Assert.That(logger.Entries.Count(e => e.StartsWith("Warning:"))).IsEqualTo(2);
        await Assert.That(string.Join("\n", logger.Entries)).DoesNotContain(Password);
    }
}
