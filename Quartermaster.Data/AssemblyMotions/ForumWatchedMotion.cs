using System;

namespace Quartermaster.Data.AssemblyMotions;

/// <param name="SeenPostNumber">Null until the topic was first looked at.</param>
public record ForumWatchedMotion(Guid MotionId, int TopicId, int CategoryId, int? SeenPostNumber, Guid AuthorMemberId);
