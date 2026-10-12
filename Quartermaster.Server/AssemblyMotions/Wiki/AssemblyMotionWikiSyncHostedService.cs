using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Quartermaster.Server.AssemblyMotions.Wiki;

/// <summary>Mirrors due assembly motions to the wiki every minute; per-iteration errors don't kill the service.</summary>
public class AssemblyMotionWikiSyncHostedService : BackgroundService {
    private static readonly TimeSpan RunInterval = TimeSpan.FromMinutes(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AssemblyMotionWikiSyncHostedService> _logger;

    public AssemblyMotionWikiSyncHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<AssemblyMotionWikiSyncHostedService> logger) {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        _logger.LogInformation("Assembly motion wiki sync hosted service started");
        while (!stoppingToken.IsCancellationRequested) {
            try {
                using var scope = _scopeFactory.CreateScope();
                var sync = scope.ServiceProvider.GetRequiredService<AssemblyMotionWikiSync>();
                var synced = await sync.SyncDueAsync(DateTime.UtcNow, stoppingToken);
                if (synced > 0)
                    _logger.LogInformation("Synced {Count} assembly motions to the wiki", synced);
            } catch (OperationCanceledException) {
                break;
            } catch (Exception ex) {
                _logger.LogError(ex, "Assembly motion wiki sync failed");
            }
            try {
                await Task.Delay(RunInterval, stoppingToken);
            } catch (OperationCanceledException) {
                break;
            }
        }
    }
}
