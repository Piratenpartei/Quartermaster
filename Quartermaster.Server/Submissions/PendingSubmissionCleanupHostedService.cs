using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartermaster.Data.Submissions;

namespace Quartermaster.Server.Submissions;

/// <summary>
/// Periodically prunes pending submissions: unconfirmed ones past their 48h expiry and
/// confirmed ones past the same window after confirmation. Per-iteration errors don't kill
/// the service.
/// </summary>
public class PendingSubmissionCleanupHostedService : BackgroundService {
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PendingSubmissionCleanupHostedService> _logger;

    public PendingSubmissionCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<PendingSubmissionCleanupHostedService> logger) {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        while (!stoppingToken.IsCancellationRequested) {
            try {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<PendingSubmissionRepository>();
                var purged = repo.PurgeStale(DateTime.UtcNow);
                if (purged > 0) {
                    _logger.LogInformation("Pruned {Count} stale pending submission(s)", purged);
                }
            } catch (OperationCanceledException) {
                break;
            } catch (Exception ex) {
                _logger.LogError(ex, "Pending submission cleanup sweep failed");
            }
            try {
                await Task.Delay(RunInterval, stoppingToken);
            } catch (OperationCanceledException) {
                break;
            }
        }
    }
}
