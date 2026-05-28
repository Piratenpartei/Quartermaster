using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Quartermaster.Server.Notifications;

namespace Quartermaster.Server.Tests.Infrastructure;

/// <summary>
/// Test replacement for the production background queue: runs each dispatch inline and
/// synchronously in a fresh scope, so the originating request blocks until the
/// <c>NotificationLog</c> rows are written. Keeps the existing "submit then assert on
/// logs" integration tests deterministic without polling.
/// </summary>
public class InlineNotificationDispatchQueue : INotificationDispatchQueue {
    private readonly IServiceScopeFactory _scopeFactory;

    public InlineNotificationDispatchQueue(IServiceScopeFactory scopeFactory) {
        _scopeFactory = scopeFactory;
    }

    public void Enqueue(NotificationDispatchRequest request) {
        using var scope = _scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<NotificationDispatcher>();
        dispatcher.DispatchAsync(
            request.TriggerId,
            request.Payload,
            request.ModelFactory,
            request.SourceEntityType,
            request.SourceEntityId,
            CancellationToken.None).GetAwaiter().GetResult();
    }
}
