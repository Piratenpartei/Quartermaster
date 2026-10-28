using System;
using System.Collections.Generic;

namespace Quartermaster.Server.Notifications;

/// <summary>Payload for <see cref="NotificationTriggers.ApplicantRegisterHit"/>. Templates see it as <c>hit</c>.</summary>
/// <param name="Path">The application page, or the first entry's page when no application is kept.</param>
/// <param name="Entries">Ready-made lines, one per matching register entry.</param>
public record ApplicantRegisterHitPayload(
    Guid RootChapterId,
    string FirstName,
    string LastName,
    string City,
    string Path,
    IReadOnlyList<string> Entries
);
