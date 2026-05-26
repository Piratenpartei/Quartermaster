using System.Collections.Generic;

namespace Quartermaster.Server.Notifications;

/// <summary>
/// Resolves the list of recipients for one trigger. The implementation knows the
/// trigger's payload shape (carried opaquely as an <c>object</c>) and which permission
/// / role joins yield the right user set.
/// </summary>
public interface IRecipientResolver {
    /// <summary>Trigger this resolver handles — must match a constant in <see cref="NotificationTriggers"/>.</summary>
    string TriggerId { get; }

    /// <summary>Resolve recipients for the given payload. Empty list = no one to notify.</summary>
    IReadOnlyList<NotificationRecipient> Resolve(object payload);
}
