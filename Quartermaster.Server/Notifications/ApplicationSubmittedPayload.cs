using System;

namespace Quartermaster.Server.Notifications;

/// <summary>Payload for <see cref="NotificationTriggers.ApplicationSubmitted"/>.</summary>
public record ApplicationSubmittedPayload(
    Guid ApplicationId,
    Guid ChapterId,
    string ChapterName,
    string ApplicantFirstName,
    string ApplicantLastName,
    bool HasReducedDueSelection
);
