using System.Collections.Generic;
using Quartermaster.Server.Notifications;

namespace Quartermaster.Server.Tests.Infrastructure;

public class CapturingNotificationQueue : INotificationDispatchQueue {
    public List<NotificationDispatchRequest> Requests { get; } = [];

    public void Enqueue(NotificationDispatchRequest request) => Requests.Add(request);
}
