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
        var changes = new ChangeResult();

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbContext>();
        var adminDivRepo = scope.ServiceProvider.GetRequiredService<AdministrativeDivisionRepository>();

        var baseFile = "DE_Base.txt";
        var postcodeFile = "DE_PostCodes.txt";

        if (!File.Exists(baseFile) || !File.Exists(postcodeFile)) {
            _logger.LogWarning("Admin division files not found: {Base}, {PostCodes}", baseFile, postcodeFile);
            HasCompletedInitialLoad = true;
            return CreateLog("", 0, new ChangeResult(), 1, "Admin division files not found", 0);
        }

        var fileHash = ComputeFileHash(baseFile, postcodeFile);
        if (_lastFileHash == fileHash) {
            _logger.LogDebug("Admin division files unchanged, skipping import");
            HasCompletedInitialLoad = true;
            return CreateLog(fileHash, 0, new ChangeResult(), 0, null, 0);
        }

        List<AdministrativeDivision> parsed;
        try {
            parsed = AdministrativeDivisionLoader.Parse(baseFile, postcodeFile);
        } catch (Exception ex) {
            _logger.LogError(ex, "Failed to parse admin division files");
            HasCompletedInitialLoad = true;
            return CreateLog(fileHash, 0, new ChangeResult(), 1, ex.Message, sw.ElapsedMilliseconds);
        }

        var existingCount = context.AdministrativeDivisions.Count();

        if (existingCount == 0) {
            // Fresh load — just bulk insert
            _logger.LogInformation("Loading {Count} admin divisions (initial load)", parsed.Count);
            adminDivRepo.CreateBulk(parsed);
            changes.Added = parsed.Count;
        } else {
            // Change detection — compare parsed data with existing DB records
            changes = ApplyChanges(context, adminDivRepo, parsed, errors);
        }

        sw.Stop();
        _lastFileHash = fileHash;
        HasCompletedInitialLoad = true;

        var log = CreateLog(fileHash, parsed.Count, changes,
            errors.Count, errors.Count > 0 ? string.Join("\n", errors) : null, sw.ElapsedMilliseconds);

        // Persist log
        context.Insert(log);

        _logger.LogInformation(
            "Admin division import complete: {Total} total, {Added} added, {Updated} updated, {Removed} removed, {Remapped} remapped, {Orphaned} orphaned, {Errors} errors in {Duration}ms",
            parsed.Count, changes.Added, changes.Updated, changes.Removed, changes.Remapped, changes.Orphaned,
            errors.Count, sw.ElapsedMilliseconds);

        return log;
    }

    private ChangeResult ApplyChanges(
        DbContext context,
        AdministrativeDivisionRepository adminDivRepo,
        List<AdministrativeDivision> parsed,
        List<string> errors) {

        var result = new ChangeResult();

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

        // Build lookup from parsed Id to parsed division (for parent resolution)
        var parsedById = parsed.ToDictionary(p => p.Id, p => p);

        // Find new divisions (parsed list is ordered by depth, so parents are inserted first)
        foreach (var (adminCode, div) in parsedByAdminCode) {
            if (!existingByAdminCode.ContainsKey(adminCode)) {
                RemapParentIdToExistingDb(div, parsedById, existingByAdminCode);
                adminDivRepo.Create(div);
                result.Added++;
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
                result.Updated++;
            }
        }

        // Phase 1: Decide remap/orphan for removed divisions and update member/chapter references
        var toDelete = new List<(AdministrativeDivision Division, AdministrativeDivision Replacement)>();

        foreach (var (adminCode, ex) in existingByAdminCode) {
            if (parsedByAdminCode.ContainsKey(adminCode))
                continue;

            var replacement = FindReplacement(ex, existing, parsedByAdminCode, parsedByPostcode,
                existingByAdminCode, context);

            if (replacement != null) {
                var membersUpdated = context.Members
                    .Where(m => m.ResidenceAdministrativeDivisionId == ex.Id)
                    .Set(m => m.ResidenceAdministrativeDivisionId, replacement.Id)
                    .Update();

                var chaptersUpdated = context.Chapters
                    .Where(c => c.AdministrativeDivisionId == ex.Id)
                    .Set(c => c.AdministrativeDivisionId, replacement.Id)
                    .Update();

                toDelete.Add((ex, replacement));
                result.Remapped++;
                if (membersUpdated > 0 || chaptersUpdated > 0) {
                    errors.Add($"Remapped division '{ex.Name}' ({adminCode}) → '{replacement.Name}' ({replacement.AdminCode}): {membersUpdated} members, {chaptersUpdated} chapters moved");
                }
            } else {
                result.Orphaned++;
                var memberCount = context.Members
                    .Where(m => m.ResidenceAdministrativeDivisionId == ex.Id)
                    .Count();
                var chapterCount = context.Chapters
                    .Where(c => c.AdministrativeDivisionId == ex.Id)
                    .Count();

                errors.Add($"ORPHANED division '{ex.Name}' ({adminCode}): no replacement found. {memberCount} members, {chapterCount} chapters still reference it.");
            }

            result.Removed++;
        }

        // Phase 2: Delete remapped divisions (deepest first to respect FK constraints)
        foreach (var (div, replacement) in toDelete.OrderByDescending(x => x.Division.Depth)) {
            // Re-parent any remaining children (orphans or not yet deleted) to the replacement
            context.AdministrativeDivisions
                .Where(d => d.ParentId == div.Id && d.Id != div.Id)
                .Set(d => d.ParentId, replacement.Id)
                .Update();

            context.AdministrativeDivisions
                .Where(d => d.Id == div.Id)
                .Delete();
        }

        return result;
    }

    private static void RemapParentIdToExistingDb(
        AdministrativeDivision div,
        Dictionary<Guid, AdministrativeDivision> parsedById,
        Dictionary<string, AdministrativeDivision> existingByAdminCode) {

        if (!div.ParentId.HasValue)
            return;
        if (!parsedById.TryGetValue(div.ParentId.Value, out var parsedParent))
            return;
        if (string.IsNullOrEmpty(parsedParent.AdminCode))
            return;
        if (!existingByAdminCode.TryGetValue(parsedParent.AdminCode, out var dbParent))
            return;

        div.ParentId = dbParent.Id;
    }

    private static AdministrativeDivision? FindReplacement(
        AdministrativeDivision removed,
        List<AdministrativeDivision> existing,
        Dictionary<string, AdministrativeDivision> parsedByAdminCode,
        Dictionary<string, AdministrativeDivision> parsedByPostcode,
        Dictionary<string, AdministrativeDivision> existingByAdminCode,
        DbContext context) {

        // Try remapping by postcode
        if (!string.IsNullOrEmpty(removed.PostCodes)) {
            var firstPostcode = removed.PostCodes.Split(',').FirstOrDefault();
            if (firstPostcode != null && parsedByPostcode.TryGetValue(firstPostcode, out var candidate)) {
                var candidateInDb = existingByAdminCode.GetValueOrDefault(candidate.AdminCode ?? "")
                    ?? context.AdministrativeDivisions
                        .Where(d => d.AdminCode == candidate.AdminCode)
                        .FirstOrDefault();

                if (candidateInDb != null)
                    return candidateInDb;
            }
        }

        // Try parent division as fallback
        if (!removed.ParentId.HasValue)
            return null;

        var parent = existing.FirstOrDefault(e => e.Id == removed.ParentId.Value);
        if (parent != null && parsedByAdminCode.ContainsKey(parent.AdminCode ?? ""))
            return parent;

        return null;
    }

    private static AdminDivisionImportLog CreateLog(string fileHash, int total, ChangeResult changes,
        int errorCount, string? errors, long durationMs) {
        return new AdminDivisionImportLog {
            ImportedAt = DateTime.UtcNow,
            FileHash = fileHash,
            TotalRecords = total,
            AddedRecords = changes.Added,
            UpdatedRecords = changes.Updated,
            RemovedRecords = changes.Removed,
            RemappedRecords = changes.Remapped,
            OrphanedRecords = changes.Orphaned,
            ErrorCount = errorCount,
            Errors = errors,
            DurationMs = durationMs
        };
    }

    public class ChangeResult {
        public int Added { get; set; }
        public int Updated { get; set; }
        public int Removed { get; set; }
        public int Remapped { get; set; }
        public int Orphaned { get; set; }
    }
}
