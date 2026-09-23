using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.NewsPosts.Pipeline;

public class NewsPipelineHostedService : BackgroundService {
    private readonly IServiceProvider _services;
    private readonly ILogger<NewsPipelineHostedService> _logger;

    public static DateTime? LastRunAt { get; private set; }
    public static string? LastRunError { get; private set; }

    public NewsPipelineHostedService(IServiceProvider services, ILogger<NewsPipelineHostedService> logger) {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        try {
            await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
        } catch (OperationCanceledException) {
            return;
        }

        while (!stoppingToken.IsCancellationRequested) {
            var interval = TimeSpan.FromMinutes(15);
            try {
                interval = await RunCycleAsync(stoppingToken);
            } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                return;
            } catch (Exception ex) {
                LastRunError = ex.Message;
                _logger.LogError(ex, "News pipeline cycle failed");
            }

            try {
                await Task.Delay(interval, stoppingToken);
            } catch (OperationCanceledException) {
                return;
            }
        }
    }

    private async Task<TimeSpan> RunCycleAsync(CancellationToken ct) {
        using var scope = _services.CreateScope();
        var settings = NewsPostsSettings.ReadFrom(scope.ServiceProvider.GetRequiredService<OptionRepository>());
        if (!settings.Enabled || !settings.HasMistral)
            return settings.PollInterval;

        await scope.ServiceProvider.GetRequiredService<NewsPipeline>().RunOnceAsync(DateTime.UtcNow, ct);
        LastRunAt = DateTime.UtcNow;
        LastRunError = null;
        return settings.PollInterval;
    }
}
