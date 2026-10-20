using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Hosting.Internal;
using Quartermaster.Server.AssemblyMotions.OpenSlides;
using Quartermaster.Server.Tests.AssemblyMotions.Fakes;

namespace Quartermaster.Server.Tests.AssemblyMotions;

public class HttpOpenSlidesClientTests {
    private const string Password = "hunter2-secret";
    private const string Created = """[[{"id":42}]]""";

    private static OpenSlidesSettings Settings(string user = "bot", string url = "https://openslides.invalid")
        => new(url, user, Password);

    private static HttpOpenSlidesClient Client(RecordingOpenSlidesHandler handler, CapturingLogger<HttpOpenSlidesClient>? logger = null, string environment = "Production")
        => new(new HttpClient(handler), new HostingEnvironment { EnvironmentName = environment }, logger ?? new CapturingLogger<HttpOpenSlidesClient>());

    private static OpenSlidesMotionCreate Motion(string? reason = "<p>Begruendung</p>")
        => new(1, "Titel", "A1", "<p>Text</p>", reason, 7, "Max Muster");

    private static async Task<OpenSlidesException> Capture(Func<Task> action) {
        try {
            await action();
        } catch (OpenSlidesException ex) {
            return ex;
        }

        throw new InvalidOperationException("No OpenSlidesException thrown.");
    }

    [Test]
    public async Task Login_reads_header_and_cookie() {
        var handler = new RecordingOpenSlidesHandler();
        handler.EnqueueLogin("bearer abc", "cookie-xyz");
        handler.EnqueueOk(Created);
        using var client = Client(handler);

        await client.LoginAsync(Settings(), CancellationToken.None);
        await client.CreateCategoryAsync(Settings(), 1, "Kat", "K", CancellationToken.None);

        var login = handler.Requests[0];
        await Assert.That(login.Method).IsEqualTo(HttpMethod.Post);
        await Assert.That(login.Url).IsEqualTo("https://openslides.invalid/system/auth/login");
        using var body = JsonDocument.Parse(login.Body);
        await Assert.That(body.RootElement.GetProperty("username").GetString()).IsEqualTo("bot");
        await Assert.That(body.RootElement.GetProperty("password").GetString()).IsEqualTo(Password);
        await Assert.That(handler.Requests[1].Headers["Authentication"]).IsEqualTo("bearer abc");
        await Assert.That(handler.Requests[1].Headers["Cookie"]).IsEqualTo("refreshId=cookie-xyz");
    }

    [Test]
    public async Task Login_without_header_throws() {
        var handler = new RecordingOpenSlidesHandler();
        handler.Enqueue(HttpStatusCode.OK, "{}");
        using var client = Client(handler);

        var ex = await Capture(() => client.LoginAsync(Settings(), CancellationToken.None));

        await Assert.That(ex.Code).IsEqualTo("login_failed");
        await Assert.That(ex.Message).IsEqualTo("Anmeldung an OpenSlides fehlgeschlagen.");
    }

    [Test]
    public async Task Action_sends_token_and_cookie() {
        var handler = new RecordingOpenSlidesHandler();
        handler.EnqueueLogin();
        handler.EnqueueOk(Created);
        using var client = Client(handler);

        await client.CreateCategoryAsync(Settings(), 3, "Kat", "K", CancellationToken.None);

        await Assert.That(handler.Requests.Count).IsEqualTo(2);
        var action = handler.Requests[1];
        await Assert.That(action.Url).IsEqualTo("https://openslides.invalid/system/action/handle_request");
        await Assert.That(action.Headers["Authentication"]).IsEqualTo("bearer token-1");
        await Assert.That(action.Headers["Cookie"]).IsEqualTo("refreshId=refresh-1");
        using var body = JsonDocument.Parse(action.Body);
        var item = body.RootElement[0];
        await Assert.That(item.GetProperty("action").GetString()).IsEqualTo("motion_category.create");
        var data = item.GetProperty("data")[0];
        await Assert.That(data.GetProperty("meeting_id").GetInt32()).IsEqualTo(3);
        await Assert.That(data.GetProperty("name").GetString()).IsEqualTo("Kat");
        await Assert.That(data.GetProperty("prefix").GetString()).IsEqualTo("K");
    }

    [Test]
    public async Task Create_motion_body_uses_snake_case_and_omits_empty_reason() {
        var handler = new RecordingOpenSlidesHandler();
        handler.EnqueueLogin();
        handler.EnqueueOk(Created);
        handler.EnqueueOk(Created);
        using var client = Client(handler);

        await client.CreateMotionAsync(Settings(), Motion(), CancellationToken.None);
        await client.CreateMotionAsync(Settings(), Motion(""), CancellationToken.None);

        using var withReason = JsonDocument.Parse(handler.Requests[1].Body);
        var data = withReason.RootElement[0].GetProperty("data")[0];
        await Assert.That(withReason.RootElement[0].GetProperty("action").GetString()).IsEqualTo("motion.create");
        await Assert.That(data.GetProperty("meeting_id").GetInt32()).IsEqualTo(1);
        await Assert.That(data.GetProperty("title").GetString()).IsEqualTo("Titel");
        await Assert.That(data.GetProperty("number").GetString()).IsEqualTo("A1");
        await Assert.That(data.GetProperty("text").GetString()).IsEqualTo("<p>Text</p>");
        await Assert.That(data.GetProperty("reason").GetString()).IsEqualTo("<p>Begruendung</p>");
        await Assert.That(data.GetProperty("category_id").GetInt32()).IsEqualTo(7);
        await Assert.That(data.GetProperty("additional_submitter").GetString()).IsEqualTo("Max Muster");

        using var withoutReason = JsonDocument.Parse(handler.Requests[2].Body);
        await Assert.That(withoutReason.RootElement[0].GetProperty("data")[0].TryGetProperty("reason", out _)).IsFalse();
    }

    [Test]
    public async Task Update_motion_body_uses_snake_case() {
        var handler = new RecordingOpenSlidesHandler();
        handler.EnqueueLogin();
        handler.EnqueueOk("[[{}]]");
        using var client = Client(handler);

        await client.UpdateMotionAsync(Settings(), new OpenSlidesMotionUpdate(9, "T", "<p>X</p>", null, "Max"), CancellationToken.None);

        using var body = JsonDocument.Parse(handler.Requests[1].Body);
        await Assert.That(body.RootElement[0].GetProperty("action").GetString()).IsEqualTo("motion.update");
        var data = body.RootElement[0].GetProperty("data")[0];
        await Assert.That(data.GetProperty("id").GetInt32()).IsEqualTo(9);
        await Assert.That(data.GetProperty("additional_submitter").GetString()).IsEqualTo("Max");
        await Assert.That(data.TryGetProperty("reason", out _)).IsFalse();
        await Assert.That(data.TryGetProperty("number", out _)).IsFalse();
    }

    [Test]
    public async Task Created_id_read_from_results() {
        var handler = new RecordingOpenSlidesHandler();
        handler.EnqueueLogin();
        handler.EnqueueOk("""[[{"id":123,"sequential_number":4}]]""");
        using var client = Client(handler);

        var id = await client.CreateMotionAsync(Settings(), Motion(), CancellationToken.None);

        await Assert.That(id).IsEqualTo(123);
    }

    [Test]
    public async Task New_authentication_header_replaces_token() {
        var handler = new RecordingOpenSlidesHandler();
        handler.EnqueueLogin();
        handler.EnqueueOk(Created, "bearer token-2");
        handler.EnqueueOk(Created);
        using var client = Client(handler);

        await client.CreateMotionAsync(Settings(), Motion(), CancellationToken.None);
        await client.CreateMotionAsync(Settings(), Motion(), CancellationToken.None);

        await Assert.That(handler.Requests.Count).IsEqualTo(3);
        await Assert.That(handler.Requests[2].Headers["Authentication"]).IsEqualTo("bearer token-2");
    }

    [Test]
    public async Task Unauthorized_triggers_one_relogin() {
        var handler = new RecordingOpenSlidesHandler();
        handler.EnqueueLogin("bearer old", "r-old");
        handler.Enqueue(HttpStatusCode.Unauthorized, "{}");
        handler.EnqueueLogin("bearer new", "r-new");
        handler.EnqueueOk(Created);
        using var client = Client(handler);

        var id = await client.CreateMotionAsync(Settings(), Motion(), CancellationToken.None);

        await Assert.That(id).IsEqualTo(42);
        await Assert.That(handler.Requests.Count).IsEqualTo(4);
        await Assert.That(handler.Requests[3].Headers["Authentication"]).IsEqualTo("bearer new");
        await Assert.That(handler.Requests[3].Headers["Cookie"]).IsEqualTo("refreshId=r-new");
    }

    [Test]
    public async Task Second_unauthorized_throws() {
        var handler = new RecordingOpenSlidesHandler();
        handler.EnqueueLogin();
        handler.Enqueue(HttpStatusCode.Unauthorized, "{}");
        handler.EnqueueLogin();
        handler.Enqueue(HttpStatusCode.Unauthorized, "{}");
        using var client = Client(handler);

        var ex = await Capture(() => client.CreateMotionAsync(Settings(), Motion(), CancellationToken.None));

        await Assert.That(ex.Code).IsEqualTo("unauthorized");
        await Assert.That(ex.Message).IsEqualTo("OpenSlides hat die Anmeldung abgelehnt.");
        await Assert.That(handler.Requests.Count).IsEqualTo(4);
    }

    [Test]
    public async Task Success_false_with_status_200_throws_action_failed_with_message() {
        var handler = new RecordingOpenSlidesHandler();
        handler.EnqueueLogin();
        handler.Enqueue(HttpStatusCode.OK, """{"success":false,"message":"Kategorie existiert bereits"}""");
        using var client = Client(handler);

        var ex = await Capture(() => client.CreateMotionAsync(Settings(), Motion(), CancellationToken.None));

        await Assert.That(ex.Code).IsEqualTo("action_failed");
        await Assert.That(ex.Message).IsEqualTo("Kategorie existiert bereits");
    }

    [Test]
    public async Task Bad_request_with_json_message_throws_action_failed_with_message() {
        var handler = new RecordingOpenSlidesHandler();
        handler.EnqueueLogin();
        handler.Enqueue(HttpStatusCode.BadRequest, """{"success": false, "message": "Nummer bereits vergeben"}""");
        using var client = Client(handler);

        var ex = await Capture(() => client.CreateMotionAsync(Settings(), Motion(), CancellationToken.None));

        await Assert.That(ex.Code).IsEqualTo("action_failed");
        await Assert.That(ex.Message).IsEqualTo("Nummer bereits vergeben");
    }

    [Test]
    public async Task Redirect_response_throws_http_error() {
        var handler = new RecordingOpenSlidesHandler();
        handler.EnqueueLogin();
        handler.Enqueue(HttpStatusCode.Found, "");
        using var client = Client(handler);

        var ex = await Capture(() => client.CreateMotionAsync(Settings(), Motion(), CancellationToken.None));

        await Assert.That(ex.Code).IsEqualTo("http_error");
    }

    [Test]
    public async Task Redirect_on_login_throws_http_error() {
        var handler = new RecordingOpenSlidesHandler();
        handler.Enqueue(HttpStatusCode.Found, "");
        using var client = Client(handler);

        var ex = await Capture(() => client.LoginAsync(Settings(), CancellationToken.None));

        await Assert.That(ex.Code).IsEqualTo("http_error");
    }

    [Test]
    public async Task Server_error_without_json_message_throws_http_error() {
        var handler = new RecordingOpenSlidesHandler();
        handler.EnqueueLogin();
        handler.Enqueue(HttpStatusCode.InternalServerError, "Internal Server Error");
        using var client = Client(handler);

        var ex = await Capture(() => client.CreateMotionAsync(Settings(), Motion(), CancellationToken.None));

        await Assert.That(ex.Code).IsEqualTo("http_error");
        await Assert.That(ex.Message).Contains("HTTP 500");
        await Assert.That(ex.Message).Contains("Internal Server Error");
    }

    [Test]
    public async Task Insecure_url_is_refused_without_a_request() {
        var handler = new RecordingOpenSlidesHandler();
        using var client = Client(handler);

        var login = await Capture(() => client.LoginAsync(Settings(url: "http://openslides.invalid"), CancellationToken.None));
        var action = await Capture(() => client.CreateMotionAsync(Settings(url: "ftp://openslides.invalid"), Motion(), CancellationToken.None));

        await Assert.That(login.Code).IsEqualTo("insecure_url");
        await Assert.That(action.Code).IsEqualTo("insecure_url");
        await Assert.That(handler.Requests.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments("http://localhost:8000")]
    [Arguments("http://127.0.0.1:8000")]
    [Arguments("http://[::1]:8000")]
    public async Task Http_is_allowed_for_loopback_in_development(string url) {
        var handler = new RecordingOpenSlidesHandler();
        handler.EnqueueLogin();
        using var client = Client(handler, environment: "Development");

        await client.LoginAsync(Settings(url: url), CancellationToken.None);

        await Assert.That(handler.Requests.Count).IsEqualTo(1);
    }

    [Test]
    [Arguments("http://localhost:8000")]
    [Arguments("http://127.0.0.1:8000")]
    [Arguments("http://[::1]:8000")]
    public async Task Http_to_loopback_is_refused_outside_development(string url) {
        var handler = new RecordingOpenSlidesHandler();
        using var client = Client(handler);

        var ex = await Capture(() => client.LoginAsync(Settings(url: url), CancellationToken.None));

        await Assert.That(ex.Code).IsEqualTo("insecure_url");
        await Assert.That(handler.Requests.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments("not json at all")]
    [Arguments("[1,2]")]
    [Arguments("{}")]
    [Arguments("""{"success":"yes"}""")]
    [Arguments("""{"success":true}""")]
    [Arguments("""{"success":true,"results":[]}""")]
    [Arguments("""{"success":true,"results":[[]]}""")]
    [Arguments("""{"success":true,"results":"x"}""")]
    [Arguments("""{"success":true,"results":[[{"name":"a"}]]}""")]
    [Arguments("""{"success":true,"results":[[{"id":"7"}]]}""")]
    [Arguments("""{"success":true,"results":[[5]]}""")]
    public async Task Malformed_response_throws_unexpected_response(string body) {
        var handler = new RecordingOpenSlidesHandler();
        handler.EnqueueLogin();
        handler.Enqueue(HttpStatusCode.OK, body);
        using var client = Client(handler);

        var ex = await Capture(() => client.CreateMotionAsync(Settings(), Motion(), CancellationToken.None));

        await Assert.That(ex.Code).IsEqualTo("unexpected_response");
    }

    [Test]
    public async Task Non_success_status_throws_http_error_with_truncated_body() {
        var handler = new RecordingOpenSlidesHandler();
        handler.EnqueueLogin();
        handler.Enqueue(HttpStatusCode.InternalServerError, new string('x', 2000));
        using var client = Client(handler);

        var ex = await Capture(() => client.CreateMotionAsync(Settings(), Motion(), CancellationToken.None));

        await Assert.That(ex.Code).IsEqualTo("http_error");
        await Assert.That(ex.Message).Contains("HTTP 500");
        await Assert.That(ex.Message.Length).IsLessThan(700);
    }

    [Test]
    public async Task Settings_change_forces_new_login() {
        var handler = new RecordingOpenSlidesHandler();
        handler.EnqueueLogin();
        handler.EnqueueOk(Created);
        handler.EnqueueOk(Created);
        handler.EnqueueLogin("bearer other", "r-other");
        handler.EnqueueOk(Created);
        handler.EnqueueLogin("bearer third", "r-third");
        handler.EnqueueOk(Created);
        using var client = Client(handler);

        await client.CreateMotionAsync(Settings(), Motion(), CancellationToken.None);
        await client.CreateMotionAsync(Settings(), Motion(), CancellationToken.None);
        await client.CreateMotionAsync(Settings("other"), Motion(), CancellationToken.None);
        await client.CreateMotionAsync(Settings("other", "https://other.invalid"), Motion(), CancellationToken.None);

        var logins = handler.Requests.Count(r => r.Url.EndsWith("/system/auth/login"));
        await Assert.That(logins).IsEqualTo(3);
        await Assert.That(handler.Requests[4].Headers["Authentication"]).IsEqualTo("bearer other");
    }

    [Test]
    public async Task Timeout_throws_timeout_code() {
        var handler = new RecordingOpenSlidesHandler();
        handler.EnqueueThrow(new TaskCanceledException("timeout"));
        using var client = Client(handler);

        var ex = await Capture(() => client.LoginAsync(Settings(), CancellationToken.None));

        await Assert.That(ex.Code).IsEqualTo("timeout");
    }

    [Test]
    public async Task Caller_cancellation_in_flight_propagates_and_releases_the_gate() {
        var handler = new BlockingHandler();
        using var client = new HttpOpenSlidesClient(new HttpClient(handler), new HostingEnvironment { EnvironmentName = "Production" }, new CapturingLogger<HttpOpenSlidesClient>());
        using var cts = new CancellationTokenSource();

        var pending = client.LoginAsync(Settings(), cts.Token);
        await handler.Started.Task;
        await cts.CancelAsync();

        OperationCanceledException? cancelled = null;
        try {
            await pending;
        } catch (OperationCanceledException ex) {
            cancelled = ex;
        }

        await Assert.That(cancelled).IsNotNull();

        handler.Blocking = false;
        await client.LoginAsync(Settings(), CancellationToken.None);
    }

    [Test]
    public async Task Refresh_id_takes_last_non_empty_cookie_and_ignores_clearing_cookie() {
        var handler = new RecordingOpenSlidesHandler();
        handler.Enqueue(HttpStatusCode.OK, "{}", "bearer t", "real-1; Path=/");
        handler.EnqueueOk(Created);
        using var client = Client(handler);

        await client.LoginAsync(Settings(), CancellationToken.None);
        await client.CreateMotionAsync(Settings(), Motion(), CancellationToken.None);

        await Assert.That(handler.Requests[1].Headers["Cookie"]).IsEqualTo("refreshId=real-1");
    }

    [Test]
    public async Task Refresh_id_uses_later_valid_cookie_after_clearing_cookie() {
        var handler = new MultiCookieHandler(["refreshId=; Max-Age=0", "other=x", "refreshId=\"quoted\"; Path=/", "refreshId=; Max-Age=0"]);
        using var client = new HttpOpenSlidesClient(new HttpClient(handler), new HostingEnvironment { EnvironmentName = "Production" }, new CapturingLogger<HttpOpenSlidesClient>());

        await client.CreateMotionAsync(Settings(), Motion(), CancellationToken.None);

        await Assert.That(handler.ActionCookie).IsEqualTo("refreshId=\"quoted\"");
    }

    [Test]
    [Arguments("a b")]
    [Arguments("a,b")]
    [Arguments("a\u0001b")]
    [Arguments("\u00e4")]
    public async Task Unsafe_refresh_id_is_treated_as_absent(string value) {
        var handler = new MultiCookieHandler([$"refreshId={value}; Path=/"]);
        using var client = new HttpOpenSlidesClient(new HttpClient(handler), new HostingEnvironment { EnvironmentName = "Production" }, new CapturingLogger<HttpOpenSlidesClient>());

        await client.CreateMotionAsync(Settings(), Motion(), CancellationToken.None);

        await Assert.That(handler.ActionCookie).IsNull();
    }

    [Test]
    public async Task Secrets_never_appear_in_exceptions_or_logs() {
        const string Token = "bearer SECRET-TOKEN";
        const string Refresh = "SECRET-REFRESH";
        var logger = new CapturingLogger<HttpOpenSlidesClient>();
        var handler = new RecordingOpenSlidesHandler();
        handler.Enqueue(HttpStatusCode.OK, "{}");
        handler.EnqueueLogin(Token, Refresh);
        handler.Enqueue(HttpStatusCode.Unauthorized, "{}");
        handler.EnqueueLogin(Token, Refresh);
        handler.Enqueue(HttpStatusCode.Unauthorized, "{}");
        handler.Enqueue(HttpStatusCode.InternalServerError, "oops");
        handler.Enqueue(HttpStatusCode.OK, """{"success":false,"message":"abgelehnt"}""");
        handler.Enqueue(HttpStatusCode.OK, "garbage");
        using var client = Client(handler, logger);

        var failures = new List<(string Code, OpenSlidesException Exception)> {
            ("login_failed", await Capture(() => client.LoginAsync(Settings(), CancellationToken.None))),
            ("unauthorized", await Capture(() => client.CreateMotionAsync(Settings(), Motion(), CancellationToken.None))),
            ("http_error", await Capture(() => client.CreateMotionAsync(Settings(), Motion(), CancellationToken.None))),
            ("action_failed", await Capture(() => client.CreateMotionAsync(Settings(), Motion(), CancellationToken.None))),
            ("unexpected_response", await Capture(() => client.CreateMotionAsync(Settings(), Motion(), CancellationToken.None))),
            ("insecure_url", await Capture(() => client.CreateMotionAsync(Settings(url: "http://openslides.invalid"), Motion(), CancellationToken.None)))
        };

        foreach (var (code, failure) in failures) {
            await Assert.That(failure.Code).IsEqualTo(code);
            foreach (var secret in new[] { Password, Token, Refresh, "SECRET-TOKEN" }) {
                await Assert.That(failure.Message).DoesNotContain(secret);
                await Assert.That(failure.ToString()).DoesNotContain(secret);
            }
        }

        foreach (var secret in new[] { Password, Token, Refresh, "SECRET-TOKEN" }) {
            await Assert.That(logger.Entries.Any(e => e.Contains(secret))).IsFalse();
        }

        await Assert.That(Settings().ToString()).DoesNotContain(Password);
    }

    private class BlockingHandler : HttpMessageHandler {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Blocking { get; set; } = true;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            if (Blocking) {
                Started.TrySetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent("{}")
            };
            response.Headers.TryAddWithoutValidation("Authentication", "bearer t");
            return response;
        }
    }

    private class MultiCookieHandler(string[] cookies) : HttpMessageHandler {
        public string? ActionCookie { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            if (request.RequestUri!.AbsolutePath.EndsWith("/login")) {
                response.Content = new StringContent("{}");
                response.Headers.TryAddWithoutValidation("Authentication", "bearer t");
                response.Headers.TryAddWithoutValidation("Set-Cookie", cookies);
            } else {
                ActionCookie = request.Headers.TryGetValues("Cookie", out var values) ? string.Join(",", values) : null;
                response.Content = new StringContent("""{"success":true,"results":[[{"id":1}]]}""");
            }

            return Task.FromResult(response);
        }
    }

    private static OpenSlidesPollCreate Poll() {
        return new OpenSlidesPollCreate(3, "SÄA001: Debatte fortsetzen?", "topic/9", "Y",
            [new OpenSlidesPollOption("Ja", null), new OpenSlidesPollOption("Nein", null)], 5, 1, 1, 1, false, false, false, "Y");
    }

    [Test]
    public async Task Poll_create_body_is_pseudoanonymous_with_entitled_group() {
        var handler = new RecordingOpenSlidesHandler();
        handler.EnqueueLogin();
        handler.EnqueueOk(Created);
        using var client = Client(handler);

        var id = await client.CreatePollAsync(Settings(), Poll(), CancellationToken.None);

        await Assert.That(id).IsEqualTo(42);
        using var body = JsonDocument.Parse(handler.Requests[1].Body);
        var item = body.RootElement[0];
        await Assert.That(item.GetProperty("action").GetString()).IsEqualTo("poll.create");
        var data = item.GetProperty("data")[0];
        await Assert.That(data.GetProperty("type").GetString()).IsEqualTo("pseudoanonymous");
        await Assert.That(data.GetProperty("pollmethod").GetString()).IsEqualTo("Y");
        await Assert.That(data.GetProperty("content_object_id").GetString()).IsEqualTo("topic/9");
        await Assert.That(data.GetProperty("entitled_group_ids")[0].GetInt32()).IsEqualTo(5);
        await Assert.That(data.GetProperty("options")[1].GetProperty("text").GetString()).IsEqualTo("Nein");
        await Assert.That(data.GetProperty("backend").GetString()).IsEqualTo("fast");
        await Assert.That(data.GetProperty("onehundred_percent_base").GetString()).IsEqualTo("Y");
    }

    [Test]
    public async Task Motion_poll_option_uses_content_object() {
        var handler = new RecordingOpenSlidesHandler();
        handler.EnqueueLogin();
        handler.EnqueueOk(Created);
        using var client = Client(handler);
        var poll = Poll() with { ContentObjectId = "motion/12", PollMethod = "YNA", Options = [new OpenSlidesPollOption(null, "motion/12")] };

        await client.CreatePollAsync(Settings(), poll, CancellationToken.None);

        using var body = JsonDocument.Parse(handler.Requests[1].Body);
        var option = body.RootElement[0].GetProperty("data")[0].GetProperty("options")[0];
        await Assert.That(option.GetProperty("content_object_id").GetString()).IsEqualTo("motion/12");
        await Assert.That(option.TryGetProperty("text", out _)).IsFalse();
    }

    [Test]
    [Arguments("poll.start")]
    [Arguments("poll.stop")]
    [Arguments("poll.publish")]
    [Arguments("poll.reset")]
    [Arguments("poll.delete")]
    public async Task Poll_lifecycle_actions_send_id(string action) {
        var handler = new RecordingOpenSlidesHandler();
        handler.EnqueueLogin();
        handler.EnqueueOk("[null]");
        using var client = Client(handler);

        var call = action switch {
            "poll.start" => client.StartPollAsync(Settings(), 17, CancellationToken.None),
            "poll.stop" => client.StopPollAsync(Settings(), 17, CancellationToken.None),
            "poll.publish" => client.PublishPollAsync(Settings(), 17, CancellationToken.None),
            "poll.reset" => client.ResetPollAsync(Settings(), 17, CancellationToken.None),
            _ => client.DeletePollAsync(Settings(), 17, CancellationToken.None)
        };
        await call;

        using var body = JsonDocument.Parse(handler.Requests[1].Body);
        await Assert.That(body.RootElement[0].GetProperty("action").GetString()).IsEqualTo(action);
        await Assert.That(body.RootElement[0].GetProperty("data")[0].GetProperty("id").GetInt32()).IsEqualTo(17);
    }

    [Test]
    public async Task Countdown_update_body() {
        var handler = new RecordingOpenSlidesHandler();
        handler.EnqueueLogin();
        handler.EnqueueOk("[null]");
        using var client = Client(handler);

        await client.UpdateCountdownAsync(Settings(), 4, true, 1800000070.5, CancellationToken.None);

        using var body = JsonDocument.Parse(handler.Requests[1].Body);
        var data = body.RootElement[0].GetProperty("data")[0];
        await Assert.That(body.RootElement[0].GetProperty("action").GetString()).IsEqualTo("projector_countdown.update");
        await Assert.That(data.GetProperty("id").GetInt32()).IsEqualTo(4);
        await Assert.That(data.GetProperty("running").GetBoolean()).IsTrue();
        await Assert.That(data.GetProperty("countdown_time").GetDouble()).IsEqualTo(1800000070.5);
    }

    [Test]
    public async Task Topic_create_body_and_id() {
        var handler = new RecordingOpenSlidesHandler();
        handler.EnqueueLogin();
        handler.EnqueueOk(Created);
        using var client = Client(handler);

        var id = await client.CreateTopicAsync(Settings(), 3, "SÄA001: Verfahren", 55, CancellationToken.None);

        await Assert.That(id).IsEqualTo(42);
        using var body = JsonDocument.Parse(handler.Requests[1].Body);
        var data = body.RootElement[0].GetProperty("data")[0];
        await Assert.That(data.GetProperty("agenda_parent_id").GetInt32()).IsEqualTo(55);
        await Assert.That(data.GetProperty("agenda_type").GetString()).IsEqualTo("common");
    }

    [Test]
    public async Task Session_tokens_log_in_once() {
        var handler = new RecordingOpenSlidesHandler();
        handler.EnqueueLogin("bearer abc", "cookie-xyz");
        using var client = Client(handler);

        var first = await client.GetSessionTokensAsync(Settings(), CancellationToken.None);
        var second = await client.GetSessionTokensAsync(Settings(), CancellationToken.None);

        await Assert.That(handler.Requests.Count).IsEqualTo(1);
        await Assert.That(first.AccessToken).IsEqualTo("bearer abc");
        await Assert.That(second.RefreshId).IsEqualTo("cookie-xyz");
    }

    [Test]
    public async Task Reported_token_replaces_stored_token() {
        var handler = new RecordingOpenSlidesHandler();
        handler.EnqueueLogin("bearer old");
        handler.EnqueueOk("[null]");
        using var client = Client(handler);
        await client.GetSessionTokensAsync(Settings(), CancellationToken.None);

        client.ReportRefreshedToken("bearer new");
        await client.StartPollAsync(Settings(), 1, CancellationToken.None);

        await Assert.That(handler.Requests[1].Headers["Authentication"]).IsEqualTo("bearer new");
    }
}
