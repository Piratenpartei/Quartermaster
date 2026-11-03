using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Quartermaster.Server.Events.Subscriptions;

/// <summary>Sends due event reminders every minute; per-iteration errors don't kill the service.</summary>
public class EventReminderHostedService : BackgroundService {
    private static readonly TimeSpan RunInterval = TimeSpan.FromMinutes(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EventReminderHostedService> _logger;

    public EventReminderHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<EventReminderHostedService> logger) {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        _logger.LogInformation("Event reminder hosted service started");
        while (!stoppingToken.IsCancellationRequested) {
            try {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<EventReminderService>();
                var sent = service.SendDue(DateTime.UtcNow);
                if (sent > 0)
                    _logger.LogInformation("Sent {Count} event reminders", sent);
            } catch (OperationCanceledException) {
                break;
            } catch (Exception ex) {
                _logger.LogError(ex, "Event reminder sweep failed");
            }
            try {
                await Task.Delay(RunInterval, stoppingToken);
            } catch (OperationCanceledException) {
                break;
            }
        }
    }
}
