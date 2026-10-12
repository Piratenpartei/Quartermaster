using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Quartermaster.Server.AssemblyMotions;

/// <summary>Lapses overdue assembly motions every few minutes; per-iteration errors don't kill the service.</summary>
public class AssemblyMotionDeadlineHostedService : BackgroundService {
    private static readonly TimeSpan RunInterval = TimeSpan.FromMinutes(5);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AssemblyMotionDeadlineHostedService> _logger;

    public AssemblyMotionDeadlineHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<AssemblyMotionDeadlineHostedService> logger) {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        _logger.LogInformation("Assembly motion deadline hosted service started");
        while (!stoppingToken.IsCancellationRequested) {
            try {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<AssemblyMotionService>();
                var reminded = service.SendCollectingReminders(DateTime.UtcNow);
                if (reminded > 0)
                    _logger.LogInformation("Sent {Count} assembly motion collecting reminders", reminded);
                var lapsed = service.LapseOverdue(DateTime.UtcNow);
                if (lapsed > 0)
                    _logger.LogInformation("Lapsed {Count} overdue assembly motions", lapsed);
            } catch (OperationCanceledException) {
                break;
            } catch (Exception ex) {
                _logger.LogError(ex, "Assembly motion deadline sweep failed");
            }
            try {
                await Task.Delay(RunInterval, stoppingToken);
            } catch (OperationCanceledException) {
                break;
            }
        }
    }
}
