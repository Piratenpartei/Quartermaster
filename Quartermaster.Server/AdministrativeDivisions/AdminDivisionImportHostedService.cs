using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Quartermaster.Server.AdministrativeDivisions;

public class AdminDivisionImportHostedService : BackgroundService {
    private readonly AdminDivisionImportService _importService;
    private readonly ILogger<AdminDivisionImportHostedService> _logger;

    public AdminDivisionImportHostedService(
        AdminDivisionImportService importService,
        ILogger<AdminDivisionImportHostedService> logger) {
        _importService = importService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        _logger.LogInformation("Admin division import service started");

        // Initial load shortly after startup
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

        while (!stoppingToken.IsCancellationRequested) {
            try {
                _importService.Import();
            } catch (Exception ex) {
                _logger.LogError(ex, "Error in admin division import");
            }

            // Poll daily
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
