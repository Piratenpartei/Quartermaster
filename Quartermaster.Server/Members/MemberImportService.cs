using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartermaster.Data;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Members;
using Quartermaster.Data.Tokens;
using Quartermaster.Data.Users;

namespace Quartermaster.Server.Members;

public class MemberImportService {
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MemberImportService> _logger;

    public MemberImportService(IServiceScopeFactory scopeFactory, ILogger<MemberImportService> logger) {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public static string ComputeFileHash(string filePath) {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Inserts a pending log row, runs the import, updates the same row on completion. Returns
    /// the pending log so a caller that doesn't want to block can hand the ID to a poller.
    /// </summary>
    public MemberImportLog StartBackgroundImport(string filePath, string? displayFileName, bool deleteAfter) {
        var pending = CreatePendingLog(filePath, displayFileName);
        SafeInsertImportLog(pending);
        _ = Task.Run(() => {
            try {
                RunImport(pending, filePath);
            } finally {
                if (deleteAfter) {
                    SafeDeleteTempFile(filePath);
                }
            }
        });
        return pending;
    }

    public MemberImportLog ImportFromFile(string filePath, string? displayFileName = null) {
        var pending = CreatePendingLog(filePath, displayFileName);
        SafeInsertImportLog(pending);
        RunImport(pending, filePath);
        return pending;
    }

    private MemberImportLog CreatePendingLog(string filePath, string? displayFileName) => new() {
        ImportedAt = DateTime.UtcNow,
        FileName = displayFileName ?? Path.GetFileName(filePath),
        FileHash = SafeComputeFileHash(filePath),
        DurationMs = MemberImportLog.PendingDurationMs
    };

    private void RunImport(MemberImportLog log, string filePath) {
        var sw = Stopwatch.StartNew();
        var errors = new List<string>();
        var totalRecords = 0;
        var newRecords = 0;
        var updatedRecords = 0;
        var now = log.ImportedAt;
        var committed = false;

        try {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbContext>();
            var memberRepo = scope.ServiceProvider.GetRequiredService<MemberRepository>();
            var chapterRepo = scope.ServiceProvider.GetRequiredService<ChapterRepository>();
            var adminDivRepo = scope.ServiceProvider.GetRequiredService<AdministrativeDivisionRepository>();
            var userRepo = scope.ServiceProvider.GetRequiredService<UserRepository>();
            var tokenRepo = scope.ServiceProvider.GetRequiredService<TokenRepository>();

            var allChapters = chapterRepo.GetAll();
            var allChapterIds = allChapters.Select(c => c.Id).ToList();
            var codesByChapter = chapterRepo.LoadExternalCodesFor(allChapterIds);
            var chaptersByExtCode = new Dictionary<string, List<Chapter>>();
            foreach (var chapter in allChapters) {
                if (!codesByChapter.TryGetValue(chapter.Id, out var codes))
                    continue;
                foreach (var code in codes) {
                    if (!chaptersByExtCode.TryGetValue(code, out var list)) {
                        list = [];
                        chaptersByExtCode[code] = list;
                    }
                    list.Add(chapter);
                }
            }

            var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture) {
                Delimiter = ";",
                HasHeaderRecord = true,
                MissingFieldFound = null,
                BadDataFound = null,
            };

            using var reader = new StreamReader(filePath, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            using var csv = new CsvReader(reader, csvConfig);
            csv.Context.RegisterClassMap<MemberCsvRecordMap>();

            var records = csv.GetRecords<MemberCsvRecord>();

            // Without this, a mid-import crash leaves partial member writes and no log to prove they came from a failed run.
            using var tx = context.BeginTransaction();

            foreach (var record in records) {
                totalRecords++;
                if (record.USER_Mitgliedsnummer == null) {
                    // Floating applicant: accepted by a chapter but first dues unpaid, no member number assigned yet.
                    _logger.LogInformation("Row {Row}: skipping Floating applicant (no member number)", totalRecords);
                    continue;
                }
                try {
                    var member = MapRecordToMember(record, chaptersByExtCode, codesByChapter, allChapters, adminDivRepo, now);
                    var existing = memberRepo.GetByMemberNumber(member.MemberNumber);

                    if (existing != null) {
                        member.Id = existing.Id;
                        // Preserve SSO link — the CSV doesn't know about local user accounts.
                        member.UserId = existing.UserId;

                        if (existing.UserId.HasValue && existing.Email != member.Email && !string.IsNullOrEmpty(member.Email))
                            userRepo.UpdateEmail(existing.UserId.Value, member.Email);

                        // Cut session access the moment a member's exit date arrives (not when it's just scheduled for the future).
                        if (existing.UserId.HasValue && !existing.IsExited(now) && member.IsExited(now))
                            tokenRepo.DeleteAllForUser(existing.UserId.Value);

                        if (memberRepo.Update(member))
                            updatedRecords++;
                    } else {
                        memberRepo.Insert(member);
                        newRecords++;
                    }
                } catch (Exception ex) {
                    errors.Add($"Row {totalRecords}: Member #{record.USER_Mitgliedsnummer} - {ex.Message}");
                    _logger.LogWarning(ex, "Failed to import member #{MemberNumber}", record.USER_Mitgliedsnummer);
                }
            }

            tx.Commit();
            committed = true;
        } catch (Exception ex) {
            errors.Add($"Import aborted at row {totalRecords}: {ex.GetType().Name}: {ex.Message}");
            _logger.LogError(ex, "Member import aborted after {Processed} rows; transaction rolled back", totalRecords);
        }

        sw.Stop();

        if (!committed) {
            newRecords = 0;
            updatedRecords = 0;
        }

        log.TotalRecords = totalRecords;
        log.NewRecords = newRecords;
        log.UpdatedRecords = updatedRecords;
        log.ErrorCount = errors.Count;
        log.Errors = errors.Count > 0 ? JsonSerializer.Serialize(errors) : null;
        log.DurationMs = sw.ElapsedMilliseconds;

        SafeUpdateImportLog(log);

        _logger.LogInformation(
            "Import complete: {Total} records ({New} new, {Updated} updated, {Errors} errors) in {Duration}ms",
            totalRecords, newRecords, updatedRecords, errors.Count, sw.ElapsedMilliseconds);
    }

    private string SafeComputeFileHash(string filePath) {
        try {
            return ComputeFileHash(filePath);
        } catch (Exception ex) {
            _logger.LogWarning(ex, "Could not hash import file {Path}", filePath);
            return "";
        }
    }

    private void SafeDeleteTempFile(string filePath) {
        try {
            if (File.Exists(filePath))
                File.Delete(filePath);
        } catch (Exception ex) {
            _logger.LogWarning(ex, "Could not delete temp import file {Path}", filePath);
        }
    }

    private void SafeUpdateImportLog(MemberImportLog log) {
        try {
            using var scope = _scopeFactory.CreateScope();
            scope.ServiceProvider.GetRequiredService<MemberRepository>().UpdateImportLog(log);
        } catch (Exception ex) {
            _logger.LogError(ex, "Could not update member import log {Id}; outcome only available in server logs", log.Id);
        }
    }

    private void SafeInsertImportLog(MemberImportLog log) {
        try {
            using var scope = _scopeFactory.CreateScope();
            scope.ServiceProvider.GetRequiredService<MemberRepository>().InsertImportLog(log);
        } catch (Exception ex) {
            _logger.LogError(ex, "Could not persist member import log; outcome only available in server logs");
        }
    }

    private static Member MapRecordToMember(
        MemberCsvRecord record,
        Dictionary<string, List<Chapter>> chaptersByExtCode,
        Dictionary<Guid, List<string>> codesByChapter,
        List<Chapter> allChapters,
        AdministrativeDivisionRepository adminDivRepo,
        DateTime importedAt) {

        var member = new Member {
            MemberNumber = record.USER_Mitgliedsnummer!.Value,
            AdmissionReference = NullIfEmpty(record.USER_refAufnahme),
            FirstName = record.Name2,
            LastName = record.Name1,
            Street = NullIfEmpty(record.LieferStrasse),
            Country = NullIfEmpty(record.LieferLand),
            PostCode = NullIfEmpty(record.LieferPLZ),
            City = NullIfEmpty(record.LieferOrt),
            Phone = NullIfEmpty(record.Telefon),
            Email = NullIfEmpty(record.Email),
            DateOfBirth = ParseDateTime(record.USER_Geburtsdatum),
            Citizenship = NullIfEmpty(record.USER_Staatsbuergerschaft),
            MembershipFee = ParseDecimal(record.USER_Beitrag),
            ReducedFee = ParseDecimal(record.USER_redBeitrag),
            FirstFee = ParseNullableDecimal(record.USER_Erstbeitrag),
            OpenFeeTotal = ParseNullableDecimal(record.USER_zoffenerbeitragtotal),
            ReducedFeeEnd = ParseDateTime(record.USER_redBeitragEnde),
            EntryDate = ParseDateTime(record.USER_Eintrittsdatum),
            ExitDate = ParseDateTime(record.USER_Austrittsdatum),
            FederalState = NullIfEmpty(record.USER_Bundesland),
            County = NullIfEmpty(record.USER_Landkreis),
            Municipality = NullIfEmpty(record.USER_Gemeinde),
            IsPending = ParseBool(record.USER_Schwebend),
            HasVotingRights = ParseBool(record.USER_zStimmberechtigung),
            ReceivesSurveys = ParseBool(record.USER_Umfragen),
            ReceivesActions = ParseBool(record.USER_Aktionen),
            ReceivesNewsletter = ParseBool(record.USER_Newsletter),
            PostBounce = ParseBool(record.USER_Postbounce),
            LastImportedAt = importedAt
        };

        // Resolve chapter from CSV hierarchy
        member.ChapterId = ResolveChapter(codesByChapter,
            NullIfEmpty(record.USER_LV),
            NullIfEmpty(record.USER_Bezirk),
            NullIfEmpty(record.USER_Kreis),
            chaptersByExtCode, allChapters);

        // Resolve residence administrative division from PLZ
        if (!string.IsNullOrEmpty(member.PostCode)) {
            var (results, _) = adminDivRepo.Search(member.PostCode, 1, 10);
            if (results.Count == 1) {
                member.ResidenceAdministrativeDivisionId = results[0].Id;
            } else if (results.Count > 1 && !string.IsNullOrEmpty(member.City)) {
                var cityMatch = results.FirstOrDefault(r =>
                    r.Name.Contains(member.City, StringComparison.OrdinalIgnoreCase));
                member.ResidenceAdministrativeDivisionId = cityMatch?.Id ?? results[0].Id;
            } else if (results.Count > 1) {
                member.ResidenceAdministrativeDivisionId = results[0].Id;
            }
        }

        return member;
    }

    private static Guid? ResolveChapter(
        Dictionary<Guid, List<string>> codesByChapter,
        string? lv, string? bezirk, string? kreis,
        Dictionary<string, List<Chapter>> chaptersByExtCode,
        List<Chapter> allChapters) {

        // Try Kreis first (most specific)
        if (kreis != null && chaptersByExtCode.TryGetValue(kreis, out var kreisChapters)) {
            foreach (var kreisChapter in kreisChapters) {
                var parent = allChapters.FirstOrDefault(c => c.Id == kreisChapter.ParentChapterId);
                if (parent == null)
                    continue;

                if (bezirk != null) {
                    if (HasCode(codesByChapter, parent.Id, bezirk)) {
                        var grandparent = allChapters.FirstOrDefault(c => c.Id == parent.ParentChapterId);
                        if (grandparent != null && HasCode(codesByChapter, grandparent.Id, lv))
                            return kreisChapter.Id;
                    }
                } else {
                    if (HasCode(codesByChapter, parent.Id, lv))
                        return kreisChapter.Id;
                }
            }
        }

        // Try Bezirk
        if (bezirk != null && chaptersByExtCode.TryGetValue(bezirk, out var bezirkChapters)) {
            foreach (var bezirkChapter in bezirkChapters) {
                var parent = allChapters.FirstOrDefault(c => c.Id == bezirkChapter.ParentChapterId);
                if (parent != null && HasCode(codesByChapter, parent.Id, lv))
                    return bezirkChapter.Id;
            }
        }

        if (lv != null && chaptersByExtCode.TryGetValue(lv, out var lvChapters))
            return lvChapters.FirstOrDefault()?.Id;

        // Fallback: every member belongs to *some* chapter — if the CSV didn't identify
        // a state/district/county, attribute them to the federal root (Direktmitglied).
        return allChapters.FirstOrDefault(c => c.ParentChapterId == null)?.Id;
    }

    private static bool HasCode(Dictionary<Guid, List<string>> codesByChapter, Guid chapterId, string? code)
        => code != null
            && codesByChapter.TryGetValue(chapterId, out var codes)
            && codes.Contains(code);

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) || value == "NULL" ? null : value.Trim();

    private static DateTime? ParseDateTime(string? value) {
        if (string.IsNullOrWhiteSpace(value) || value == "NULL")
            return null;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
            return result;
        return null;
    }

    private static decimal ParseDecimal(string? value) {
        if (string.IsNullOrWhiteSpace(value) || value == "NULL")
            return 0;
        if (decimal.TryParse(value, CultureInfo.InvariantCulture, out var result))
            return result;
        return 0;
    }

    private static decimal? ParseNullableDecimal(string? value) {
        if (string.IsNullOrWhiteSpace(value) || value == "NULL")
            return null;
        if (decimal.TryParse(value, CultureInfo.InvariantCulture, out var result))
            return result;
        return null;
    }

    private static bool ParseBool(string? value)
        => value == "1";
}
