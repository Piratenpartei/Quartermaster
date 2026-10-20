using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Quartermaster.Server.Forwarding;

/// <summary>Runs the forwarding job every minute; per-iteration errors don't kill the service.</summary>
public class ForwardingJobHostedService : BackgroundService {
    private static readonly TimeSpan RunInterval = TimeSpan.FromMinutes(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ForwardingJobHostedService> _logger;

    public ForwardingJobHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<ForwardingJobHostedService> logger) {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        _logger.LogInformation("Forwarding job hosted service started");
        while (!stoppingToken.IsCancellationRequested) {
            try {
                using var scope = _scopeFactory.CreateScope();
                var job = scope.ServiceProvider.GetRequiredService<ForwardingJob>();
                var completed = await job.RunAsync(DateTime.UtcNow, stoppingToken);
                if (completed > 0)
                    _logger.LogInformation("Completed {Count} forwarding steps", completed);
            } catch (OperationCanceledException) {
                break;
            } catch (Exception ex) {
                _logger.LogError(ex, "Forwarding job run failed");
            }
            try {
                await Task.Delay(RunInterval, stoppingToken);
            } catch (OperationCanceledException) {
                break;
            }
        }
    }
}
