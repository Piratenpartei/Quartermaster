using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Quartermaster.Server.AssemblyMotions.Forum;

/// <summary>Mirrors due assembly motions to the forum every minute; per-iteration errors don't kill the service.</summary>
public class AssemblyMotionForumSyncHostedService : BackgroundService {
    private static readonly TimeSpan RunInterval = TimeSpan.FromMinutes(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AssemblyMotionForumSyncHostedService> _logger;

    public AssemblyMotionForumSyncHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<AssemblyMotionForumSyncHostedService> logger) {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        _logger.LogInformation("Assembly motion forum sync hosted service started");
        while (!stoppingToken.IsCancellationRequested) {
            try {
                using var scope = _scopeFactory.CreateScope();
                var sync = scope.ServiceProvider.GetRequiredService<AssemblyMotionForumSync>();
                var synced = await sync.SyncDueAsync(DateTime.UtcNow, stoppingToken);
                if (synced > 0)
                    _logger.LogInformation("Synced {Count} assembly motions to the forum", synced);
            } catch (OperationCanceledException) {
                break;
            } catch (Exception ex) {
                _logger.LogError(ex, "Assembly motion forum sync failed");
            }
            try {
                await Task.Delay(RunInterval, stoppingToken);
            } catch (OperationCanceledException) {
                break;
            }
        }
    }
}
