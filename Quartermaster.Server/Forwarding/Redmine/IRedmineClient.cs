using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Server.Forwarding.Redmine;

/// <summary>Redmine REST calls of the forwarding job and the setup assistants. Every failure surfaces as a <see cref="RedmineException"/>.</summary>
public interface IRedmineClient {
    Task<string> GetCurrentUserNameAsync(RedmineConnectionSettings settings, CancellationToken ct);

    Task<List<RedmineIdName>> ListProjectsAsync(RedmineConnectionSettings settings, CancellationToken ct);

    Task<List<RedmineIdName>> ListTrackersAsync(RedmineConnectionSettings settings, CancellationToken ct);

    Task<List<RedmineIdName>> ListIssueStatusesAsync(RedmineConnectionSettings settings, CancellationToken ct);

    Task<List<RedmineIdName>> ListIssuePrioritiesAsync(RedmineConnectionSettings settings, CancellationToken ct);

    Task<int> CreateIssueAsync(RedmineConnectionSettings settings, RedmineIssueCreate issue, CancellationToken ct);

    Task UpdateIssueAsync(RedmineConnectionSettings settings, int issueId, RedmineIssueUpdate update, CancellationToken ct);

    /// <summary>The id of an issue in the project whose description contains <paramref name="reference"/>, in any status; null when there is none.</summary>
    Task<int?> FindIssueByReferenceAsync(RedmineConnectionSettings settings, int projectId, string reference, CancellationToken ct);
}
