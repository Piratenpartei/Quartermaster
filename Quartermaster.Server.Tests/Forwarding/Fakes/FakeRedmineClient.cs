using Quartermaster.Server.Forwarding.Redmine;

namespace Quartermaster.Server.Tests.Forwarding.Fakes;

/// <summary>In-memory Redmine instances keyed by base URL. Static so the test and the service see the same state; test classes run <c>[NotInParallel("FakeRedmineClient")]</c> and call <see cref="Reset"/>.</summary>
public class FakeRedmineClient : IRedmineClient {
    private static readonly Lock StateLock = new();
    private static int nextId = 1000;

    public static List<FakeRedmineIssue> Issues { get; } = [];
    public static List<string> Calls { get; } = [];

    /// <summary>Exceptions thrown by method name (CurrentUser, Projects, Trackers, Statuses, Priorities, Create, Update, Find) on every call.</summary>
    public static Dictionary<string, Exception> ThrowOn { get; } = [];

    /// <summary>When set, Create stores the issue and then throws this, like a timeout after Redmine already created the ticket.</summary>
    public static Exception? ThrowAfterCreate { get; set; }

    /// <summary>Runs with the method name at the start of every call, before <see cref="ThrowOn"/>; lets a test act while a request is in progress.</summary>
    public static Action<string>? BeforeCall { get; set; }

    public static void Reset() {
        using (StateLock.EnterScope()) {
            Issues.Clear();
            Calls.Clear();
            ThrowOn.Clear();
            ThrowAfterCreate = null;
            BeforeCall = null;
        }
    }

    public static int Count(string method) {
        using (StateLock.EnterScope()) {
            return Calls.Count(c => c == method);
        }
    }

    public Task<string> GetCurrentUserNameAsync(RedmineConnectionSettings settings, CancellationToken ct) {
        Record("CurrentUser");
        return Task.FromResult("qm-bot");
    }

    public Task<List<RedmineIdName>> ListProjectsAsync(RedmineConnectionSettings settings, CancellationToken ct) {
        Record("Projects");
        return Task.FromResult(new List<RedmineIdName> { new(1, "Mitgliederverwaltung"), new(2, "Beitragsminderungen") });
    }

    public Task<List<RedmineIdName>> ListTrackersAsync(RedmineConnectionSettings settings, CancellationToken ct) {
        Record("Trackers");
        return Task.FromResult(new List<RedmineIdName> { new(3, "Antrag") });
    }

    public Task<List<RedmineIdName>> ListIssueStatusesAsync(RedmineConnectionSettings settings, CancellationToken ct) {
        Record("Statuses");
        return Task.FromResult(new List<RedmineIdName> { new(4, "Neu"), new(5, "Zur Eintragung") });
    }

    public Task<List<RedmineIdName>> ListIssuePrioritiesAsync(RedmineConnectionSettings settings, CancellationToken ct) {
        Record("Priorities");
        return Task.FromResult(new List<RedmineIdName> { new(6, "Normal") });
    }

    public Task<int> CreateIssueAsync(RedmineConnectionSettings settings, RedmineIssueCreate issue, CancellationToken ct) {
        Record("Create");
        FakeRedmineIssue created;
        using (StateLock.EnterScope()) {
            created = new FakeRedmineIssue {
                Id = nextId++,
                BaseUrl = settings.Root,
                ProjectId = issue.ProjectId,
                TrackerId = issue.TrackerId,
                Subject = issue.Subject,
                Description = issue.Description,
                PriorityId = issue.PriorityId,
                AssigneeId = issue.AssigneeId,
                CustomFields = issue.CustomFields.ToList()
            };
            Issues.Add(created);
        }

        var after = ThrowAfterCreate;
        if (after != null)
            throw after;
        return Task.FromResult(created.Id);
    }

    public Task UpdateIssueAsync(RedmineConnectionSettings settings, int issueId, RedmineIssueUpdate update, CancellationToken ct) {
        Record("Update");
        using (StateLock.EnterScope()) {
            var issue = Issues.Single(i => i.Id == issueId && i.BaseUrl == settings.Root);
            issue.Notes.Add(update.Notes);
            issue.StatusId = update.StatusId ?? issue.StatusId;
            issue.AssigneeId = update.AssigneeId ?? issue.AssigneeId;
        }

        return Task.CompletedTask;
    }

    public Task<int?> FindIssueByReferenceAsync(RedmineConnectionSettings settings, int projectId, string reference, CancellationToken ct) {
        Record("Find");
        using (StateLock.EnterScope()) {
            var issue = Issues.FirstOrDefault(i => i.BaseUrl == settings.Root && i.ProjectId == projectId && i.Description.Contains(reference));
            return Task.FromResult(issue?.Id);
        }
    }

    private static void Record(string method) {
        BeforeCall?.Invoke(method);

        Exception? exception;
        using (StateLock.EnterScope()) {
            Calls.Add(method);
            ThrowOn.TryGetValue(method, out exception);
        }

        if (exception != null)
            throw exception;
    }
}

public class FakeRedmineIssue {
    public int Id { get; set; }
    public string BaseUrl { get; set; } = "";
    public int ProjectId { get; set; }
    public int TrackerId { get; set; }
    public string Subject { get; set; } = "";
    public string Description { get; set; } = "";
    public int? PriorityId { get; set; }
    public int? AssigneeId { get; set; }
    public int? StatusId { get; set; }
    public List<RedmineCustomFieldValue> CustomFields { get; set; } = [];
    public List<string> Notes { get; } = [];
}
