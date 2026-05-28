namespace Quartermaster.Server.Notifications;

/// <summary>
/// Hands a notification dispatch off the request thread. Production enqueues to a
/// background drainer; tests run it inline for deterministic assertions.
/// </summary>
public interface INotificationDispatchQueue {
    void Enqueue(NotificationDispatchRequest request);
}
