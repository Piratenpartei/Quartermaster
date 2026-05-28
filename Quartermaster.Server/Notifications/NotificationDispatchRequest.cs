using System;
using System.Collections.Generic;

namespace Quartermaster.Server.Notifications;

/// <summary>
/// A queued notification dispatch. <see cref="ModelFactory"/> and <see cref="Payload"/>
/// must capture only plain data (no request-scoped services) — the background drainer
/// runs them in a fresh scope after the originating request has completed.
/// </summary>
public record NotificationDispatchRequest(
    string TriggerId,
    object Payload,
    Func<NotificationRecipient, Dictionary<string, object>> ModelFactory,
    string? SourceEntityType,
    Guid? SourceEntityId
);
