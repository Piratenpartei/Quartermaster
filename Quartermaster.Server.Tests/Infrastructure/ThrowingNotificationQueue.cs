using System;
using Quartermaster.Server.Notifications;

namespace Quartermaster.Server.Tests.Infrastructure;

public class ThrowingNotificationQueue : INotificationDispatchQueue {
    public int Attempts { get; private set; }

    public void Enqueue(NotificationDispatchRequest request) {
        Attempts++;
        throw new InvalidOperationException("queue is down");
    }
}
