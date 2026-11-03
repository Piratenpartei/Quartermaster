using System;

namespace Quartermaster.Data.AssemblyMotions;

/// <param name="SeenPostNumber">Null until the topic was first looked at.</param>
/// <param name="AuthorUserId">Null when the author's member has no user account.</param>
public record ForumWatchedMotion(Guid MotionId, int TopicId, int CategoryId, int? SeenPostNumber, Guid AuthorMemberId, Guid EventId, Guid? AuthorUserId);
