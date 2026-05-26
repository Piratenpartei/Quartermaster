using System;

namespace Quartermaster.Server.Email;

/// <summary>In-memory queue payload between <c>EmailMessageChannel</c> and the background sender.</summary>
public record EmailMessage(
    Guid NotificationLogId,
    string To,
    string Subject,
    string Body
);
