using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartermaster.Data.NewsPosts;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.NewsPosts.Programme;

public class ProgrammeSyncHostedService : BackgroundService {
    private static readonly TimeSpan Interval = TimeSpan.FromDays(7);

    private readonly IServiceProvider _services;
    private readonly ILogger<ProgrammeSyncHostedService> _logger;

    public ProgrammeSyncHostedService(IServiceProvider services, ILogger<ProgrammeSyncHostedService> logger) {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        using (var scope = _services.CreateScope()) {
            scope.ServiceProvider.GetRequiredService<ProgrammeSyncService>().LoadIndex();
        }

        while (!stoppingToken.IsCancellationRequested) {
            try {
                await RunIfDueAsync(stoppingToken);
            } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                return;
            } catch (Exception ex) {
                _logger.LogError(ex, "Programme sync cycle failed");
            }

            try {
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            } catch (OperationCanceledException) {
                return;
            }
        }
    }

    // Gates on the most recent successful sync, not the oldest one: MarkSourceFailed never
    // touches LastSyncedAt, so a source that never succeeds would otherwise pin the gate at
    // DateTime.MinValue and force every source to resync every hour instead of weekly. A
    // restart must not immediately resync (checked hourly, so a missed week still catches up
    // within an hour), and a wiki outage must not permanently block resyncing once nothing has
    // ever succeeded.
    public static bool IsSyncDue(IReadOnlyCollection<ProgrammeSource> sources, DateTime nowUtc) {
        if (sources.Count == 0)
            return false;

        var lastSuccess = sources.Max(s => s.LastSyncedAt);
        if (lastSuccess == null)
            return true;

        return nowUtc - lastSuccess.Value >= Interval;
    }

    private async Task RunIfDueAsync(CancellationToken ct) {
        using var scope = _services.CreateScope();
        var settings = NewsPostsSettings.ReadFrom(scope.ServiceProvider.GetRequiredService<OptionRepository>());
        if (!settings.Enabled || !settings.HasMistral)
            return;

        var repo = scope.ServiceProvider.GetRequiredService<ProgrammeRepository>();
        var sources = repo.GetSources();
        if (!IsSyncDue(sources, DateTime.UtcNow))
            return;

        await scope.ServiceProvider.GetRequiredService<ProgrammeSyncService>().SyncAllAsync(ct);
    }
}
