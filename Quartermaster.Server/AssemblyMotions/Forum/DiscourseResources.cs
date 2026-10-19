namespace Quartermaster.Server.AssemblyMotions.Forum;

public record DiscourseTopic(int TopicId, int FirstPostId, string Title, bool Deleted);

public record DiscoursePost(int PostId, string Raw, bool Deleted);

/// <param name="IsStaff">Moderator or admin; staff accounts are exempt from Discourse's per-day topic and edit limits.</param>
public record DiscourseCurrentUser(string Username, bool IsStaff);

/// <param name="CanCreateTopic">The current user's permission on the category is "full" (create, reply, see).</param>
public record DiscourseCategory(int Id, string Name, bool ReadRestricted, bool CanCreateTopic);
