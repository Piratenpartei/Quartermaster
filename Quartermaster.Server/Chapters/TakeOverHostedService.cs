using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Quartermaster.Server.Chapters;

/// <summary>Moves overdue application motions up every 15 minutes; per-iteration errors don't kill the service.</summary>
public class TakeOverHostedService : BackgroundService {
    private static readonly TimeSpan RunInterval = TimeSpan.FromMinutes(15);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TakeOverHostedService> _logger;

    public TakeOverHostedService(IServiceScopeFactory scopeFactory, ILogger<TakeOverHostedService> logger) {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        _logger.LogInformation("Take-over hosted service started");
        while (!stoppingToken.IsCancellationRequested) {
            try {
                using var scope = _scopeFactory.CreateScope();
                var moved = scope.ServiceProvider.GetRequiredService<TakeOverSweep>().Run(DateTime.UtcNow);
                if (moved > 0)
                    _logger.LogInformation("Moved {Count} application motions up after their deadline", moved);
            } catch (OperationCanceledException) {
                break;
            } catch (Exception ex) {
                _logger.LogError(ex, "Take-over sweep failed");
            }
            try {
                await Task.Delay(RunInterval, stoppingToken);
            } catch (OperationCanceledException) {
                break;
            }
        }
    }
}
