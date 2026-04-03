using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
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

    public MemberImportLog ImportFromFile(string filePath) {
        var sw = Stopwatch.StartNew();
        var fileName = Path.GetFileName(filePath);
        var fileHash = ComputeFileHash(filePath);
        var errors = new List<string>();
        int totalRecords = 0, newRecords = 0, updatedRecords = 0;

        using var scope = _scopeFactory.CreateScope();
        var memberRepo = scope.ServiceProvider.GetRequiredService<MemberRepository>();
        var chapterRepo = scope.ServiceProvider.GetRequiredService<ChapterRepository>();
        var adminDivRepo = scope.ServiceProvider.GetRequiredService<AdministrativeDivisionRepository>();
        var userRepo = scope.ServiceProvider.GetRequiredService<UserRepository>();
        var tokenRepo = scope.ServiceProvider.GetRequiredService<TokenRepository>();

        // Pre-load chapter lookup: all chapters with ExternalCode
        var allChapters = chapterRepo.GetAll();
        var chaptersByExtCode = allChapters
            .Where(c => c.ExternalCode != null)
            .GroupBy(c => c.ExternalCode!)
            .ToDictionary(g => g.Key, g => g.ToList());

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
        var now = DateTime.UtcNow;

        foreach (var record in records) {
            totalRecords++;
            try {
                var member = MapRecordToMember(record, chaptersByExtCode, allChapters, adminDivRepo, now);
                var existing = memberRepo.GetByMemberNumber(member.MemberNumber);

                if (existing != null) {
                    member.Id = existing.Id;
                    member.UserId = existing.UserId; // Preserve SSO link

                    // Sync email to linked user if it changed
                    if (existing.UserId.HasValue && existing.EMail != member.EMail && !string.IsNullOrEmpty(member.EMail))
                        userRepo.UpdateEmail(existing.UserId.Value, member.EMail);

                    // Invalidate all tokens when a member exits the party
                    if (existing.UserId.HasValue && !existing.ExitDate.HasValue && member.ExitDate.HasValue)
                        tokenRepo.DeleteAllForUser(existing.UserId.Value);

                    memberRepo.Update(member);
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

        sw.Stop();

        var log = new MemberImportLog {
            ImportedAt = now,
            FileName = fileName,
            FileHash = fileHash,
            TotalRecords = totalRecords,
            NewRecords = newRecords,
            UpdatedRecords = updatedRecords,
            ErrorCount = errors.Count,
            Errors = errors.Count > 0 ? JsonSerializer.Serialize(errors) : null,
            DurationMs = sw.ElapsedMilliseconds
        };

        memberRepo.InsertImportLog(log);

        _logger.LogInformation(
            "Import complete: {Total} records ({New} new, {Updated} updated, {Errors} errors) in {Duration}ms",
            totalRecords, newRecords, updatedRecords, errors.Count, sw.ElapsedMilliseconds);

        return log;
    }

    private static Member MapRecordToMember(
        MemberCsvRecord record,
        Dictionary<string, List<Chapter>> chaptersByExtCode,
        List<Chapter> allChapters,
        AdministrativeDivisionRepository adminDivRepo,
        DateTime importedAt) {

        var member = new Member {
            MemberNumber = record.USER_Mitgliedsnummer,
            AdmissionReference = NullIfEmpty(record.USER_refAufnahme),
            FirstName = record.Name2,
            LastName = record.Name1,
            Street = NullIfEmpty(record.LieferStrasse),
            Country = NullIfEmpty(record.LieferLand),
            PostCode = NullIfEmpty(record.LieferPLZ),
            City = NullIfEmpty(record.LieferOrt),
            Phone = NullIfEmpty(record.Telefon),
            EMail = NullIfEmpty(record.EMail),
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
        member.ChapterId = ResolveChapter(
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
                    // Kreis parent should be the Bezirk
                    if (parent.ExternalCode == bezirk) {
                        var grandparent = allChapters.FirstOrDefault(c => c.Id == parent.ParentChapterId);
                        if (grandparent?.ExternalCode == lv)
                            return kreisChapter.Id;
                    }
                } else {
                    // No Bezirk - Kreis parent should be the LV
                    if (parent.ExternalCode == lv)
                        return kreisChapter.Id;
                }
            }
        }

        // Try Bezirk
        if (bezirk != null && chaptersByExtCode.TryGetValue(bezirk, out var bezirkChapters)) {
            foreach (var bezirkChapter in bezirkChapters) {
                var parent = allChapters.FirstOrDefault(c => c.Id == bezirkChapter.ParentChapterId);
                if (parent?.ExternalCode == lv)
                    return bezirkChapter.Id;
            }
        }

        // Try LV (state)
        if (lv != null && chaptersByExtCode.TryGetValue(lv, out var lvChapters))
            return lvChapters.FirstOrDefault()?.Id;

        return null;
    }

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
