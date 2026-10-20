using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Hosting.Internal;
using Quartermaster.Server.Forwarding.Redmine;
using Quartermaster.Server.Tests.AssemblyMotions.Fakes;
using Quartermaster.Server.Tests.Forwarding.Fakes;

namespace Quartermaster.Server.Tests.Forwarding;

public class HttpRedmineClientTests {
    private const string Key = "redmine-secret-key";

    private static RedmineConnectionSettings Settings(string url = "https://redmine.invalid")
        => new(url, Key);

    private static HttpRedmineClient Client(RecordingRedmineHandler handler, CapturingLogger<HttpRedmineClient>? logger = null, string environment = "Production")
        => new(new HttpClient(handler), new HostingEnvironment { EnvironmentName = environment }, logger ?? new CapturingLogger<HttpRedmineClient>());

    private static async Task<RedmineException> Capture(Func<Task> action) {
        try {
            await action();
        } catch (RedmineException ex) {
            return ex;
        }

        throw new InvalidOperationException("No RedmineException thrown.");
    }

    [Test]
    public async Task Sends_api_key_header() {
        var handler = new RecordingRedmineHandler();
        handler.EnqueueOk("""{"user":{"id":3,"login":"qm-bot","firstname":"Q","lastname":"M"}}""");
        using var client = Client(handler);

        var name = await client.GetCurrentUserNameAsync(Settings(), CancellationToken.None);

        var request = handler.Requests.Single();
        await Assert.That(request.Url).IsEqualTo("https://redmine.invalid/users/current.json");
        await Assert.That(request.Headers["X-Redmine-API-Key"]).IsEqualTo(Key);
        await Assert.That(name).IsEqualTo("qm-bot");
    }

    [Test]
    public async Task Create_issue_body_contains_all_fields() {
        var handler = new RecordingRedmineHandler();
        handler.Enqueue(HttpStatusCode.Created, """{"issue":{"id":4711}}""");
        using var client = Client(handler);

        var id = await client.CreateIssueAsync(Settings(), new RedmineIssueCreate(5, 6, "Betreff", "Text", 2, 9, [new RedmineCustomFieldValue(11, "4711")]), CancellationToken.None);

        var request = handler.Requests.Single();
        await Assert.That(request.Method).IsEqualTo(HttpMethod.Post);
        await Assert.That(request.Url).IsEqualTo("https://redmine.invalid/issues.json");
        using var body = JsonDocument.Parse(request.Body);
        var issue = body.RootElement.GetProperty("issue");
        await Assert.That(issue.GetProperty("project_id").GetInt32()).IsEqualTo(5);
        await Assert.That(issue.GetProperty("tracker_id").GetInt32()).IsEqualTo(6);
        await Assert.That(issue.GetProperty("subject").GetString()).IsEqualTo("Betreff");
        await Assert.That(issue.GetProperty("description").GetString()).IsEqualTo("Text");
        await Assert.That(issue.GetProperty("priority_id").GetInt32()).IsEqualTo(2);
        await Assert.That(issue.GetProperty("assigned_to_id").GetInt32()).IsEqualTo(9);
        var field = issue.GetProperty("custom_fields")[0];
        await Assert.That(field.GetProperty("id").GetInt32()).IsEqualTo(11);
        await Assert.That(field.GetProperty("value").GetString()).IsEqualTo("4711");
        await Assert.That(id).IsEqualTo(4711);
    }

    [Test]
    public async Task Optional_fields_are_left_out() {
        var handler = new RecordingRedmineHandler();
        handler.Enqueue(HttpStatusCode.Created, """{"issue":{"id":1}}""");
        using var client = Client(handler);

        await client.CreateIssueAsync(Settings(), new RedmineIssueCreate(5, 6, "Betreff", "Text", null, null, []), CancellationToken.None);

        using var body = JsonDocument.Parse(handler.Requests.Single().Body);
        var issue = body.RootElement.GetProperty("issue");
        await Assert.That(issue.TryGetProperty("priority_id", out _)).IsFalse();
        await Assert.That(issue.TryGetProperty("assigned_to_id", out _)).IsFalse();
        await Assert.That(issue.TryGetProperty("custom_fields", out _)).IsFalse();
    }

    [Test]
    public async Task Update_issue_sends_notes_status_and_assignee() {
        var handler = new RecordingRedmineHandler();
        handler.Enqueue(HttpStatusCode.NoContent, "");
        using var client = Client(handler);

        await client.UpdateIssueAsync(Settings(), 4711, new RedmineIssueUpdate("Angenommen", 3, 8), CancellationToken.None);

        var request = handler.Requests.Single();
        await Assert.That(request.Method).IsEqualTo(HttpMethod.Put);
        await Assert.That(request.Url).IsEqualTo("https://redmine.invalid/issues/4711.json");
        using var body = JsonDocument.Parse(request.Body);
        var issue = body.RootElement.GetProperty("issue");
        await Assert.That(issue.GetProperty("notes").GetString()).IsEqualTo("Angenommen");
        await Assert.That(issue.GetProperty("status_id").GetInt32()).IsEqualTo(3);
        await Assert.That(issue.GetProperty("assigned_to_id").GetInt32()).IsEqualTo(8);
    }

    [Test]
    public async Task Projects_follow_paging() {
        var handler = new RecordingRedmineHandler();
        handler.EnqueueOk("""{"projects":[{"id":1,"name":"A"},{"id":2,"name":"B"}],"total_count":3,"offset":0,"limit":2}""");
        handler.EnqueueOk("""{"projects":[{"id":3,"name":"C"}],"total_count":3,"offset":2,"limit":2}""");
        using var client = Client(handler);

        var projects = await client.ListProjectsAsync(Settings(), CancellationToken.None);

        await Assert.That(projects.Select(p => p.Name).ToArray()).IsEquivalentTo(new[] { "A", "B", "C" });
        await Assert.That(handler.Requests[1].Url).Contains("offset=2");
    }

    [Test]
    public async Task Lists_read_trackers_statuses_and_priorities() {
        var handler = new RecordingRedmineHandler();
        handler.EnqueueOk("""{"trackers":[{"id":1,"name":"Antrag"}]}""");
        handler.EnqueueOk("""{"issue_statuses":[{"id":2,"name":"Neu"}]}""");
        handler.EnqueueOk("""{"issue_priorities":[{"id":3,"name":"Normal"}]}""");
        using var client = Client(handler);

        var trackers = await client.ListTrackersAsync(Settings(), CancellationToken.None);
        var statuses = await client.ListIssueStatusesAsync(Settings(), CancellationToken.None);
        var priorities = await client.ListIssuePrioritiesAsync(Settings(), CancellationToken.None);

        await Assert.That(trackers.Single().Name).IsEqualTo("Antrag");
        await Assert.That(statuses.Single().Id).IsEqualTo(2);
        await Assert.That(priorities.Single().Name).IsEqualTo("Normal");
        await Assert.That(handler.Requests[2].Url).IsEqualTo("https://redmine.invalid/enumerations/issue_priorities.json");
    }

    [Test]
    public async Task Find_by_reference_queries_description_filter() {
        var handler = new RecordingRedmineHandler();
        handler.EnqueueOk("""{"issues":[{"id":99}],"total_count":1}""");
        using var client = Client(handler);

        var id = await client.FindIssueByReferenceAsync(Settings(), 5, "QM-0A1B2C3D", CancellationToken.None);

        var url = handler.Requests.Single().Url;
        await Assert.That(url).Contains("project_id=5");
        await Assert.That(url).Contains("status_id=%2A");
        await Assert.That(url).Contains("description=~QM-0A1B2C3D");
        await Assert.That(id).IsEqualTo(99);
    }

    [Test]
    [Arguments(401, "unauthorized")]
    [Arguments(403, "unauthorized")]
    [Arguments(404, "not_found")]
    [Arguments(500, "http_error")]
    public async Task Status_codes_are_translated(int status, string code) {
        var handler = new RecordingRedmineHandler();
        handler.Enqueue((HttpStatusCode)status, "");
        using var client = Client(handler);

        var ex = await Capture(() => client.GetCurrentUserNameAsync(Settings(), CancellationToken.None));

        await Assert.That(ex.Code).IsEqualTo(code);
        await Assert.That(ex.StatusCode).IsEqualTo(status);
    }

    [Test]
    public async Task Rejected_issue_carries_redmine_errors() {
        var handler = new RecordingRedmineHandler();
        handler.Enqueue((HttpStatusCode)422, """{"errors":["Tracker ist kein gültiger Wert","Mitgliedsnummer darf nicht leer sein"]}""");
        using var client = Client(handler);

        var ex = await Capture(() => client.CreateIssueAsync(Settings(), new RedmineIssueCreate(5, 6, "S", "D", null, null, []), CancellationToken.None));

        await Assert.That(ex.Code).IsEqualTo(RedmineException.Rejected);
        await Assert.That(ex.Message).Contains("Tracker ist kein gültiger Wert");
        await Assert.That(ex.Message).Contains("Mitgliedsnummer darf nicht leer sein");
    }

    [Test]
    public async Task Insecure_url_sends_nothing() {
        var handler = new RecordingRedmineHandler();
        using var client = Client(handler);

        var ex = await Capture(() => client.GetCurrentUserNameAsync(Settings("http://redmine.invalid"), CancellationToken.None));

        await Assert.That(ex.Code).IsEqualTo(RedmineException.InsecureUrl);
        await Assert.That(handler.Requests.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Key_never_appears_in_logs_or_errors() {
        var handler = new RecordingRedmineHandler();
        handler.Enqueue((HttpStatusCode)422, $$"""{"errors":["bad {{Key}}"]}""");
        var logger = new CapturingLogger<HttpRedmineClient>();
        using var client = Client(handler, logger);

        var ex = await Capture(() => client.CreateIssueAsync(Settings(), new RedmineIssueCreate(5, 6, "S", "D", null, null, []), CancellationToken.None));

        await Assert.That(ex.Message).DoesNotContain(Key);
        await Assert.That(logger.Entries.Any(e => e.Contains(Key))).IsFalse();
        await Assert.That(Settings().ToString()).DoesNotContain(Key);
    }
    [Test]
    public async Task Plain_http_to_localhost_is_refused_outside_development() {
        var handler = new RecordingRedmineHandler();
        using var client = Client(handler);

        var ex = await Capture(() => client.GetCurrentUserNameAsync(Settings("http://localhost:8080"), CancellationToken.None));

        await Assert.That(ex.Code).IsEqualTo(RedmineException.InsecureUrl);
        await Assert.That(handler.Requests.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Plain_http_to_localhost_is_allowed_in_development() {
        var handler = new RecordingRedmineHandler();
        handler.EnqueueOk("""{"user":{"id":3,"login":"qm-bot","firstname":"Q","lastname":"M"}}""");
        using var client = Client(handler, environment: "Development");

        var name = await client.GetCurrentUserNameAsync(Settings("http://localhost:8080"), CancellationToken.None);

        await Assert.That(name).IsEqualTo("qm-bot");
    }

    [Test]
    [Arguments("https://redmine.example.org", false, true)]
    [Arguments("http://localhost:8080", true, true)]
    [Arguments("http://localhost:8080", false, false)]
    [Arguments("http://redmine.example.org", true, false)]
    public async Task Url_rule(string url, bool development, bool allowed) {
        await Assert.That(new RedmineConnectionSettings(url, Key).IsSecureUrl(development)).IsEqualTo(allowed);
    }
}
