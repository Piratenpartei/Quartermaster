using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartermaster.Data;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.SupportSignatures;
using Quartermaster.Server.SupportSignatures;

namespace Quartermaster.Server.AdministrativeDivisions;

/// <summary>
/// Keeps the official certification offices in line with the bundled address directory. Runs after the division import, because it
/// finds municipalities by their key, and again whenever the directory file or the divisions changed.
/// </summary>
public class AdministrationAddressImportService {
    /// <summary>The "Anschriften" sheet of the Destatis address directory, saved as CSV.</summary>
    public const string FileName = "AdministrationAddresses.csv";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AdminDivisionImportService _divisionImport;
    private readonly ILogger<AdministrationAddressImportService> _logger;
    private string? _lastImportKey;

    public AdministrationAddressImportService(IServiceScopeFactory scopeFactory, AdminDivisionImportService divisionImport,
        ILogger<AdministrationAddressImportService> logger) {
        _scopeFactory = scopeFactory;
        _divisionImport = divisionImport;
        _logger = logger;
    }

    public void Import(string? dataDirectory = null) {
        var path = Path.Combine(dataDirectory ?? Directory.GetCurrentDirectory(), FileName);
        if (!File.Exists(path)) {
            _logger.LogWarning("Address directory not found at {Path}; municipalities get no official office", path);
            return;
        }

        var bytes = File.ReadAllBytes(path);
        var fileHash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var importKey = $"{fileHash}|{_divisionImport.LastFileHash}";
        if (importKey == _lastImportKey)
            return;

        var sw = Stopwatch.StartNew();
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbContext>();
        try {
            var data = AdministrationAddressParser.Parse(new StringReader(CsvTextDecoder.Decode(bytes)));
            var result = OfficialOfficeSync.Apply(context, data.Offices);
            sw.Stop();
            _lastImportKey = importKey;
            PersistLog(context, new AdminDivisionImportLog {
                ImportedAt = DateTime.UtcNow,
                FileHash = fileHash,
                TotalRecords = data.Offices.Count,
                AddedRecords = result.Created,
                UpdatedRecords = result.Updated,
                RemovedRecords = result.Removed,
                Errors = Notes(data, result),
                DurationMs = sw.ElapsedMilliseconds
            });
            _logger.LogInformation(
                "Address directory import complete: {Offices} offices ({Created} added, {Updated} updated, {Removed} removed), {Assigned} municipalities in {Duration}ms",
                data.Offices.Count, result.Created, result.Updated, result.Removed, result.Assigned, sw.ElapsedMilliseconds);
        } catch (Exception ex) {
            _logger.LogError(ex, "Address directory import failed");
            PersistLog(context, new AdminDivisionImportLog {
                ImportedAt = DateTime.UtcNow,
                FileHash = fileHash,
                ErrorCount = 1,
                Errors = $"{FileName}: {ex}",
                DurationMs = sw.ElapsedMilliseconds
            });
        }
    }

    private static string Notes(AdministrationAddressData data, OfficialOfficeSyncResult result) {
        return $"{FileName}: {data.Offices.Count} Verwaltungen, {result.Assigned} Gemeinden zugeordnet, {result.UnknownMunicipalities} Gemeinden unbekannt, "
            + $"{data.WithoutAddress} ohne Anschrift, {data.TwoAdministrations} mit zwei Verwaltungen, {result.KeptForShipments} entfallene Verwaltungen wegen Sendungen behalten";
    }

    private void PersistLog(DbContext context, AdminDivisionImportLog log) {
        try {
            context.Insert(log);
        } catch (Exception ex) {
            _logger.LogError(ex, "Could not persist address directory import log");
        }
    }
}
