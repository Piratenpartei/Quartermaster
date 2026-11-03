using System.Collections.Generic;

namespace Quartermaster.Server.AssemblyMotions.Forum;

public record DiscourseTopic(int TopicId, int FirstPostId, string Title, bool Deleted, int HighestPostNumber = 1);

public record DiscoursePost(int PostId, string Raw, bool Deleted);

/// <param name="IsStaff">Moderator or admin; staff accounts are exempt from Discourse's per-day topic and edit limits.</param>
/// <param name="IsAdmin">Only an admin may look accounts up by their Keycloak id.</param>
public record DiscourseCurrentUser(string Username, bool IsStaff, bool IsAdmin = false);

/// <param name="CanCreateTopic">The current user's permission on the category is "full" (create, reply, see).</param>
public record DiscourseCategory(int Id, string Name, bool ReadRestricted, bool CanCreateTopic);

public record DiscourseTopicActivity(int TopicId, int HighestPostNumber);

/// <param name="Topics">Most recently active first.</param>
public record DiscourseTopicListPage(IReadOnlyList<DiscourseTopicActivity> Topics, bool HasMore);

/// <param name="PostType">1 regular, 2 moderator action, 3 small action (closed, pinned and the like), 4 whisper.</param>
/// <param name="UserId">Null for a post whose account was removed.</param>
public record DiscourseReply(int PostNumber, int PostType, int? UserId, string Username, bool Deleted, bool Hidden) {
    public const int RegularPostType = 1;
}
