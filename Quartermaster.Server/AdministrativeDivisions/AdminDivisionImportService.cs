using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartermaster.Data;
using Quartermaster.Data.AdministrativeDivisions;

namespace Quartermaster.Server.AdministrativeDivisions;

public class AdminDivisionImportService {
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AdminDivisionImportService> _logger;
    private string? _lastFileHash;

    public bool HasCompletedInitialLoad { get; private set; }

    public AdminDivisionImportService(IServiceScopeFactory scopeFactory, ILogger<AdminDivisionImportService> logger) {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public static string ComputeFileHash(string baseFilePath, string postcodeFilePath) {
        using var stream1 = File.OpenRead(baseFilePath);
        using var stream2 = File.OpenRead(postcodeFilePath);
        var hash1 = SHA256.HashData(stream1);
        var hash2 = SHA256.HashData(stream2);
        var combined = new byte[hash1.Length + hash2.Length];
        hash1.CopyTo(combined, 0);
        hash2.CopyTo(combined, hash1.Length);
        return Convert.ToHexStringLower(SHA256.HashData(combined));
    }

    public AdminDivisionImportLog Import() {
        var sw = Stopwatch.StartNew();
        var errors = new List<string>();
        int added = 0, updated = 0, removed = 0, remapped = 0, orphaned = 0;

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbContext>();
        var adminDivRepo = scope.ServiceProvider.GetRequiredService<AdministrativeDivisionRepository>();

        var baseFile = "DE_Base.txt";
        var postcodeFile = "DE_PostCodes.txt";

        if (!File.Exists(baseFile) || !File.Exists(postcodeFile)) {
            _logger.LogWarning("Admin division files not found: {Base}, {PostCodes}", baseFile, postcodeFile);
            HasCompletedInitialLoad = true;
            return CreateLog("", 0, 0, 0, 0, 0, 0, 1,
                "Admin division files not found", 0);
        }

        var fileHash = ComputeFileHash(baseFile, postcodeFile);
        if (_lastFileHash == fileHash) {
            _logger.LogDebug("Admin division files unchanged, skipping import");
            HasCompletedInitialLoad = true;
            return CreateLog(fileHash, 0, 0, 0, 0, 0, 0, 0, null, 0);
        }

        List<AdministrativeDivision> parsed;
        try {
            parsed = AdministrativeDivisionLoader.Parse(baseFile, postcodeFile);
        } catch (Exception ex) {
            _logger.LogError(ex, "Failed to parse admin division files");
            HasCompletedInitialLoad = true;
            return CreateLog(fileHash, 0, 0, 0, 0, 0, 0, 1, ex.Message, sw.ElapsedMilliseconds);
        }

        var existingCount = context.AdministrativeDivisions.Count();

        if (existingCount == 0) {
            // Fresh load — just bulk insert
            _logger.LogInformation("Loading {Count} admin divisions (initial load)", parsed.Count);
            adminDivRepo.CreateBulk(parsed);
            added = parsed.Count;
        } else {
            // Change detection — compare parsed data with existing DB records
            var result = ApplyChanges(context, adminDivRepo, parsed, errors);
            added = result.Added;
            updated = result.Updated;
            removed = result.Removed;
            remapped = result.Remapped;
            orphaned = result.Orphaned;
        }

        sw.Stop();
        _lastFileHash = fileHash;
        HasCompletedInitialLoad = true;

        var log = CreateLog(fileHash, parsed.Count, added, updated, removed, remapped, orphaned,
            errors.Count, errors.Count > 0 ? string.Join("\n", errors) : null, sw.ElapsedMilliseconds);

        // Persist log
        context.Insert(log);

        _logger.LogInformation(
            "Admin division import complete: {Total} total, {Added} added, {Updated} updated, {Removed} removed, {Remapped} remapped, {Orphaned} orphaned, {Errors} errors in {Duration}ms",
            parsed.Count, added, updated, removed, remapped, orphaned, errors.Count, sw.ElapsedMilliseconds);

        return log;
    }

    private (int Added, int Updated, int Removed, int Remapped, int Orphaned) ApplyChanges(
        DbContext context,
        AdministrativeDivisionRepository adminDivRepo,
        List<AdministrativeDivision> parsed,
        List<string> errors) {

        int added = 0, updated = 0, removed = 0, remapped = 0, orphaned = 0;

        var existing = context.AdministrativeDivisions.ToList();
        var existingByAdminCode = existing
            .Where(e => !string.IsNullOrEmpty(e.AdminCode))
            .ToDictionary(e => e.AdminCode!, e => e);
        var parsedByAdminCode = parsed
            .Where(p => !string.IsNullOrEmpty(p.AdminCode))
            .ToDictionary(p => p.AdminCode!, p => p);

        // Build postcode → division lookup from NEW data for remapping
        var parsedByPostcode = new Dictionary<string, AdministrativeDivision>();
        foreach (var p in parsed) {
            if (string.IsNullOrEmpty(p.PostCodes))
                continue;

            foreach (var pc in p.PostCodes.Split(',')) {
                parsedByPostcode.TryAdd(pc, p);
            }
        }

        // Find new divisions
        foreach (var (adminCode, div) in parsedByAdminCode) {
            if (!existingByAdminCode.ContainsKey(adminCode)) {
                adminDivRepo.Create(div);
                added++;
            }
        }

        // Find changed divisions (name changed, same admin code)
        foreach (var (adminCode, div) in parsedByAdminCode) {
            if (!existingByAdminCode.TryGetValue(adminCode, out var ex))
                continue;

            if (ex.Name != div.Name || ex.PostCodes != div.PostCodes) {
                context.AdministrativeDivisions
                    .Where(d => d.Id == ex.Id)
                    .Set(d => d.Name, div.Name)
                    .Set(d => d.PostCodes, div.PostCodes)
                    .Update();
                updated++;
            }
        }

        // Find removed divisions
        foreach (var (adminCode, ex) in existingByAdminCode) {
            if (parsedByAdminCode.ContainsKey(adminCode))
                continue;

            // Try remapping by postcode
            AdministrativeDivision? replacement = null;
            if (!string.IsNullOrEmpty(ex.PostCodes)) {
                var firstPostcode = ex.PostCodes.Split(',').FirstOrDefault();
                if (firstPostcode != null && parsedByPostcode.TryGetValue(firstPostcode, out var candidate)) {
                    // Verify the candidate exists in DB (may have just been added)
                    var candidateInDb = existingByAdminCode.GetValueOrDefault(candidate.AdminCode ?? "")
                        ?? context.AdministrativeDivisions
                            .Where(d => d.AdminCode == candidate.AdminCode)
                            .FirstOrDefault();

                    if (candidateInDb != null)
                        replacement = candidateInDb;
                }
            }

            // Try parent division as fallback
            if (replacement == null && ex.ParentId.HasValue) {
                var parent = existing.FirstOrDefault(e => e.Id == ex.ParentId.Value);
                if (parent != null && parsedByAdminCode.ContainsKey(parent.AdminCode ?? ""))
                    replacement = parent;
            }

            if (replacement != null) {
                // Remap members and chapters
                var membersUpdated = context.Members
                    .Where(m => m.ResidenceAdministrativeDivisionId == ex.Id)
                    .Set(m => m.ResidenceAdministrativeDivisionId, replacement.Id)
                    .Update();

                var chaptersUpdated = context.Chapters
                    .Where(c => c.AdministrativeDivisionId == ex.Id)
                    .Set(c => c.AdministrativeDivisionId, replacement.Id)
                    .Update();

                // Delete the old division
                context.AdministrativeDivisions
                    .Where(d => d.Id == ex.Id)
                    .Delete();

                remapped++;
                if (membersUpdated > 0 || chaptersUpdated > 0) {
                    errors.Add($"Remapped division '{ex.Name}' ({adminCode}) → '{replacement.Name}' ({replacement.AdminCode}): {membersUpdated} members, {chaptersUpdated} chapters moved");
                }
            } else {
                // Orphan — keep in DB, log for admin review
                orphaned++;
                var memberCount = context.Members
                    .Where(m => m.ResidenceAdministrativeDivisionId == ex.Id)
                    .Count();
                var chapterCount = context.Chapters
                    .Where(c => c.AdministrativeDivisionId == ex.Id)
                    .Count();

                errors.Add($"ORPHANED division '{ex.Name}' ({adminCode}): no replacement found. {memberCount} members, {chapterCount} chapters still reference it.");
            }

            removed++;
        }

        return (added, updated, removed, remapped, orphaned);
    }

    private static AdminDivisionImportLog CreateLog(string fileHash, int total, int added, int updated,
        int removed, int remapped, int orphaned, int errorCount, string? errors, long durationMs) {
        return new AdminDivisionImportLog {
            ImportedAt = DateTime.UtcNow,
            FileHash = fileHash,
            TotalRecords = total,
            AddedRecords = added,
            UpdatedRecords = updated,
            RemovedRecords = removed,
            RemappedRecords = remapped,
            OrphanedRecords = orphaned,
            ErrorCount = errorCount,
            Errors = errors,
            DurationMs = durationMs
        };
    }
}
