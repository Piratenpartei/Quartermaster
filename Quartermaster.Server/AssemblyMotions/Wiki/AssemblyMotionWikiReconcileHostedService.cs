using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Quartermaster.Server.AssemblyMotions.Wiki;

/// <summary>Compares the mirrored wiki pages with the stored state once a day, starting a fixed time after startup so restarts do not hit the wiki.</summary>
public class AssemblyMotionWikiReconcileHostedService : BackgroundService {
    private static readonly TimeSpan StartDelay = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AssemblyMotionWikiReconcileHostedService> _logger;

    public AssemblyMotionWikiReconcileHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<AssemblyMotionWikiReconcileHostedService> logger) {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        _logger.LogInformation("Assembly motion wiki reconcile hosted service started");
        try {
            await Task.Delay(StartDelay, stoppingToken);
        } catch (OperationCanceledException) {
            return;
        }

        while (!stoppingToken.IsCancellationRequested) {
            try {
                using var scope = _scopeFactory.CreateScope();
                var reconcile = scope.ServiceProvider.GetRequiredService<AssemblyMotionWikiReconcile>();
                await reconcile.RunAsync(DateTime.UtcNow, stoppingToken);
            } catch (OperationCanceledException) {
                break;
            } catch (Exception ex) {
                _logger.LogError(ex, "Assembly motion wiki reconcile failed");
            }
            try {
                await Task.Delay(RunInterval, stoppingToken);
            } catch (OperationCanceledException) {
                break;
            }
        }
    }
}
