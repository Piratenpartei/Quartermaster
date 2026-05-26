using System;

namespace Quartermaster.Server.Notifications;

/// <summary>Payload for <see cref="NotificationTriggers.DueSelectionSubmitted"/>.</summary>
public record DueSelectionSubmittedPayload(
    Guid DueSelectionId,
    Guid ChapterId,
    string ChapterName,
    string SubmitterFirstName,
    string SubmitterLastName,
    decimal SelectedDue
);
