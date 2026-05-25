using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Quartermaster.Server.Members;

/// <summary>Runs <see cref="RetentionAnonymizationService"/> once a day; per-iteration errors don't kill the service.</summary>
public class RetentionAnonymizationHostedService : BackgroundService {
    private static readonly TimeSpan RunInterval = TimeSpan.FromDays(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RetentionAnonymizationHostedService> _logger;

    public RetentionAnonymizationHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<RetentionAnonymizationHostedService> logger) {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        _logger.LogInformation("Retention anonymization hosted service started");
        while (!stoppingToken.IsCancellationRequested) {
            try {
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<RetentionAnonymizationService>();
                svc.RunOnce(DateTime.UtcNow);
            } catch (OperationCanceledException) {
                break;
            } catch (Exception ex) {
                _logger.LogError(ex, "Retention anonymization sweep failed");
            }
            try {
                await Task.Delay(RunInterval, stoppingToken);
            } catch (OperationCanceledException) {
                break;
            }
        }
    }
}
