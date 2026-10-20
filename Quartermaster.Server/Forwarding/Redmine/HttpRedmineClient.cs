using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Quartermaster.Server.Forwarding.Redmine;

/// <summary>
/// Redmine JSON API client. Response bodies are never logged, so neither the API key nor applicant data from a ticket ends up in the log;
/// Redmine's own validation messages are passed on with the key redacted.
/// </summary>
public class HttpRedmineClient : IRedmineClient, IDisposable {
    private const int PageSize = 100;
    private const int MaxPages = 50;

    private readonly HttpClient _http;
    private readonly bool _allowLoopbackHttp;
    private readonly ILogger<HttpRedmineClient> _logger;

    public HttpRedmineClient(HttpClient http, IHostEnvironment environment, ILogger<HttpRedmineClient> logger) {
        _http = http;
        _allowLoopbackHttp = environment.IsDevelopment();
        _http.Timeout = TimeSpan.FromSeconds(30);
        _logger = logger;
    }

    public async Task<string> GetCurrentUserNameAsync(RedmineConnectionSettings settings, CancellationToken ct) {
        using var doc = await SendAsync(settings, HttpMethod.Get, "/users/current.json", null, ct);
        var user = Property(doc.RootElement, "user");
        return StringProperty(user, "login");
    }

    public async Task<List<RedmineIdName>> ListProjectsAsync(RedmineConnectionSettings settings, CancellationToken ct) {
        var projects = new List<RedmineIdName>();
        for (var page = 0; page < MaxPages; page++) {
            using var doc = await SendAsync(settings, HttpMethod.Get, $"/projects.json?limit={PageSize}&offset={projects.Count}", null, ct);
            var items = ReadIdNames(doc.RootElement, "projects");
            projects.AddRange(items);
            var total = doc.RootElement.TryGetProperty("total_count", out var count) && count.ValueKind == JsonValueKind.Number ? count.GetInt32() : projects.Count;
            if (items.Count == 0 || projects.Count >= total)
                break;
        }

        return projects;
    }

    public Task<List<RedmineIdName>> ListTrackersAsync(RedmineConnectionSettings settings, CancellationToken ct) {
        return ListAsync(settings, "/trackers.json", "trackers", ct);
    }

    public Task<List<RedmineIdName>> ListIssueStatusesAsync(RedmineConnectionSettings settings, CancellationToken ct) {
        return ListAsync(settings, "/issue_statuses.json", "issue_statuses", ct);
    }

    public Task<List<RedmineIdName>> ListIssuePrioritiesAsync(RedmineConnectionSettings settings, CancellationToken ct) {
        return ListAsync(settings, "/enumerations/issue_priorities.json", "issue_priorities", ct);
    }

    public async Task<int> CreateIssueAsync(RedmineConnectionSettings settings, RedmineIssueCreate issue, CancellationToken ct) {
        var fields = new Dictionary<string, object> {
            ["project_id"] = issue.ProjectId,
            ["tracker_id"] = issue.TrackerId,
            ["subject"] = issue.Subject,
            ["description"] = issue.Description
        };
        if (issue.PriorityId != null)
            fields["priority_id"] = issue.PriorityId.Value;
        if (issue.AssigneeId != null)
            fields["assigned_to_id"] = issue.AssigneeId.Value;
        if (issue.CustomFields.Count > 0)
            fields["custom_fields"] = issue.CustomFields.Select(f => new Dictionary<string, object> { ["id"] = f.Id, ["value"] = f.Value }).ToList();

        using var doc = await SendAsync(settings, HttpMethod.Post, "/issues.json", new Dictionary<string, object> { ["issue"] = fields }, ct);
        return IntProperty(Property(doc.RootElement, "issue"), "id");
    }

    public async Task UpdateIssueAsync(RedmineConnectionSettings settings, int issueId, RedmineIssueUpdate update, CancellationToken ct) {
        var fields = new Dictionary<string, object> { ["notes"] = update.Notes };
        if (update.StatusId != null)
            fields["status_id"] = update.StatusId.Value;
        if (update.AssigneeId != null)
            fields["assigned_to_id"] = update.AssigneeId.Value;

        using var doc = await SendAsync(settings, HttpMethod.Put, $"/issues/{issueId}.json", new Dictionary<string, object> { ["issue"] = fields }, ct);
    }

    public async Task<int?> FindIssueByReferenceAsync(RedmineConnectionSettings settings, int projectId, string reference, CancellationToken ct) {
        var path = $"/issues.json?project_id={projectId}&status_id={Uri.EscapeDataString("*")}&description=~{Uri.EscapeDataString(reference)}&limit=1";
        using var doc = await SendAsync(settings, HttpMethod.Get, path, null, ct);
        if (!doc.RootElement.TryGetProperty("issues", out var issues) || issues.ValueKind != JsonValueKind.Array || issues.GetArrayLength() == 0)
            return null;

        return IntProperty(issues[0], "id");
    }

    public void Dispose() {
        _http.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<List<RedmineIdName>> ListAsync(RedmineConnectionSettings settings, string path, string property, CancellationToken ct) {
        using var doc = await SendAsync(settings, HttpMethod.Get, path, null, ct);
        return ReadIdNames(doc.RootElement, property);
    }

    private async Task<JsonDocument> SendAsync(RedmineConnectionSettings settings, HttpMethod method, string path, object? body, CancellationToken ct) {
        if (!settings.IsSecureUrl(_allowLoopbackHttp))
            throw new RedmineException("Die Redmine-URL muss https verwenden.", RedmineException.InsecureUrl);

        using var request = new HttpRequestMessage(method, new Uri(settings.Root + path));
        request.Headers.TryAddWithoutValidation("X-Redmine-API-Key", settings.ApiKey);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        if (body != null)
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try {
            response = await _http.SendAsync(request, ct);
        } catch (TaskCanceledException ex) when (!ct.IsCancellationRequested) {
            throw Fail(method, path, new RedmineException("Zeitüberschreitung bei der Anfrage an Redmine.", RedmineException.Timeout, inner: ex));
        } catch (HttpRequestException ex) {
            throw Fail(method, path, new RedmineException("Redmine ist nicht erreichbar.", RedmineException.HttpError, inner: ex));
        }

        using (response) {
            if (!response.IsSuccessStatusCode)
                throw Fail(method, path, await ErrorAsync(settings, response, ct));

            var text = await response.Content.ReadAsStringAsync(ct);
            try {
                return JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
            } catch (JsonException ex) {
                throw Fail(method, path, new RedmineException("Redmine hat unerwartet geantwortet.", RedmineException.InvalidResponse, (int)response.StatusCode, ex));
            }
        }
    }

    private static async Task<RedmineException> ErrorAsync(RedmineConnectionSettings settings, HttpResponseMessage response, CancellationToken ct) {
        var status = (int)response.StatusCode;
        if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            return new RedmineException("Redmine hat den API-Schlüssel abgelehnt oder das Konto hat keine Berechtigung.", RedmineException.Unauthorized, status);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return new RedmineException("Redmine kennt die Adresse oder das Objekt nicht.", RedmineException.NotFound, status);
        if (status == 422) {
            var errors = await ErrorsAsync(response, ct);
            var message = errors.Count == 0 ? "Redmine hat das Ticket abgelehnt." : "Redmine hat das Ticket abgelehnt: " + string.Join("; ", errors);
            return new RedmineException(Redact(settings, message), RedmineException.Rejected, status);
        }

        return new RedmineException("Redmine ist nicht erreichbar oder hat die Anfrage abgelehnt.", RedmineException.HttpError, status);
    }

    private static async Task<List<string>> ErrorsAsync(HttpResponseMessage response, CancellationToken ct) {
        try {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Array)
                return [];

            return errors.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)
                .ToList();
        } catch (JsonException) {
            return [];
        }
    }

    private static string Redact(RedmineConnectionSettings settings, string text) {
        return settings.ApiKey.Length == 0 ? text : text.Replace(settings.ApiKey, "***");
    }

    private RedmineException Fail(HttpMethod method, string path, RedmineException exception) {
        var pathOnly = path.Split('?')[0];
        _logger.LogWarning("Redmine request {Method} {Path} failed: {Code} (HTTP {Status})", method, pathOnly, exception.Code, exception.StatusCode);
        return exception;
    }

    private static List<RedmineIdName> ReadIdNames(JsonElement root, string property) {
        if (!root.TryGetProperty(property, out var items) || items.ValueKind != JsonValueKind.Array)
            throw new RedmineException($"Redmine hat unerwartet geantwortet (Feld {property} fehlt).", RedmineException.InvalidResponse);

        return items.EnumerateArray()
            .Select(i => new RedmineIdName(IntProperty(i, "id"), StringProperty(i, "name")))
            .ToList();
    }

    private static JsonElement Property(JsonElement element, string name) {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
            throw new RedmineException($"Redmine hat unerwartet geantwortet (Feld {name} fehlt).", RedmineException.InvalidResponse);
        return value;
    }

    private static int IntProperty(JsonElement element, string name) {
        var value = Property(element, name);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number))
            throw new RedmineException($"Redmine hat unerwartet geantwortet (Feld {name}).", RedmineException.InvalidResponse);
        return number;
    }

    private static string StringProperty(JsonElement element, string name) {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : "";
    }
}
