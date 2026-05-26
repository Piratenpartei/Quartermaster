using System;

namespace Quartermaster.Server.Notifications;

/// <summary>Payload for the <see cref="NotificationTriggers.MotionSubmitted"/> trigger.</summary>
public record MotionSubmittedPayload(
    Guid MotionId,
    Guid ChapterId,
    string Title,
    string AuthorName,
    string ChapterName
);
