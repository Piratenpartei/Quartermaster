using System;

namespace Quartermaster.Server.Notifications;

/// <summary>Payload for <see cref="NotificationTriggers.ApplicantRegisterHit"/>. Templates see it as <c>hit</c>.
/// Carries no names: mail and Telegram are unencrypted, the details are behind the link.</summary>
/// <param name="Path">The application page, or the first entry's page when no application is kept.</param>
/// <param name="EntryCount">How many register entries the application matches.</param>
public record ApplicantRegisterHitPayload(
    Guid RootChapterId,
    string Path,
    int EntryCount
);
