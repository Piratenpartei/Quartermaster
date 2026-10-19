using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Server.AssemblyMotions.Forum;

/// <summary>Discourse API calls of the forum mirror. Every failure surfaces as a <see cref="DiscourseException"/>.</summary>
public interface IDiscourseClient {
    Task<DiscourseCurrentUser> GetCurrentUserAsync(DiscourseSettings settings, CancellationToken ct);

    Task<DiscourseCategory> GetCategoryAsync(DiscourseSettings settings, int categoryId, CancellationToken ct);

    /// <summary>Returns null when no topic carries the external id.</summary>
    Task<DiscourseTopic?> FindTopicByExternalIdAsync(DiscourseSettings settings, string externalId, CancellationToken ct);

    Task<DiscourseTopic> GetTopicAsync(DiscourseSettings settings, int topicId, CancellationToken ct);

    Task<DiscoursePost> GetPostAsync(DiscourseSettings settings, int postId, CancellationToken ct);

    Task<DiscourseTopic> CreateTopicAsync(DiscourseSettings settings, string title, string raw, int categoryId, string externalId, CancellationToken ct);

    Task EditPostAsync(DiscourseSettings settings, int postId, string raw, string editReason, CancellationToken ct);

    Task UpdateTopicTitleAsync(DiscourseSettings settings, int topicId, string title, CancellationToken ct);
}
