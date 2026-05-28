using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Quartermaster.Server.Notifications;

/// <summary>
/// Drains <see cref="ChannelNotificationDispatchQueue"/>, running each dispatch in its
/// own scope (fresh DbContext) so resolution + rendering + channel sends happen off the
/// originating request thread.
/// </summary>
public class NotificationDispatchBackgroundService : BackgroundService {
    private readonly ChannelNotificationDispatchQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationDispatchBackgroundService> _logger;

    public NotificationDispatchBackgroundService(
        ChannelNotificationDispatchQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationDispatchBackgroundService> logger) {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        await foreach (var request in _queue.Reader.ReadAllAsync(stoppingToken)) {
            try {
                using var scope = _scopeFactory.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<NotificationDispatcher>();
                await dispatcher.DispatchAsync(
                    request.TriggerId,
                    request.Payload,
                    request.ModelFactory,
                    request.SourceEntityType,
                    request.SourceEntityId,
                    stoppingToken);
            } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                break;
            } catch (Exception ex) {
                _logger.LogError(ex, "Notification dispatch failed for trigger {Trigger}", request.TriggerId);
            }
        }
    }
}
