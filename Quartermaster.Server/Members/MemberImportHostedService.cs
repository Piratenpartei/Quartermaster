using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.Members;

public class MemberImportHostedService : BackgroundService {
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MemberImportService _importService;
    private readonly ILogger<MemberImportHostedService> _logger;
    private string? _lastFileHash;

    public MemberImportHostedService(
        IServiceScopeFactory scopeFactory,
        MemberImportService importService,
        ILogger<MemberImportHostedService> logger) {
        _scopeFactory = scopeFactory;
        _importService = importService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        _logger.LogInformation("Member import hosted service started");

        // Wait a bit for the app to fully initialize
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested) {
            try {
                var (filePath, intervalMinutes) = ReadOptions();

                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath)) {
                    var currentHash = MemberImportService.ComputeFileHash(filePath);

                    if (_lastFileHash == null || _lastFileHash != currentHash) {
                        _logger.LogInformation("File change detected, starting import from {Path}", filePath);
                        var log = _importService.ImportFromFile(filePath);
                        _lastFileHash = currentHash;
                        _logger.LogInformation(
                            "Import finished: {Total} total, {New} new, {Updated} updated, {Errors} errors",
                            log.TotalRecords, log.NewRecords, log.UpdatedRecords, log.ErrorCount);
                    }
                }

                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
            } catch (OperationCanceledException) {
                break;
            } catch (Exception ex) {
                _logger.LogError(ex, "Error in member import polling loop");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private (string? filePath, double intervalMinutes) ReadOptions() {
        using var scope = _scopeFactory.CreateScope();
        var optionRepo = scope.ServiceProvider.GetRequiredService<OptionRepository>();
        var chapterRepo = scope.ServiceProvider.GetRequiredService<ChapterRepository>();

        var filePath = optionRepo.ResolveValue("member_import.file_path", null, chapterRepo);
        var intervalStr = optionRepo.ResolveValue("member_import.polling_interval_minutes", null, chapterRepo);

        double interval = 10;
        if (double.TryParse(intervalStr, out var parsed) && parsed > 0)
            interval = parsed;

        return (filePath, interval);
    }
}
