using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using Quartermaster.Api.I18n;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures;

/// <summary>
/// Reads the CSV of hand-kept office addresses: Gemeinde, Gemeindeschluessel, Amt, Zusatz, Strasse_HausNr, PLZ, Ort, found by header
/// name in any order; other columns are ignored. Excel drops the leading zero of a key, so seven digits are padded.
/// </summary>
public static class CustomOfficeCsvParser {
    private const string MunicipalityColumn = "Gemeinde";
    private const string KeyColumn = "Gemeindeschluessel";
    private const string NameColumn = "Amt";
    private const string AdditionColumn = "Zusatz";
    private const string StreetColumn = "Strasse_HausNr";
    private const string PostCodeColumn = "PLZ";
    private const string CityColumn = "Ort";
    private const int KeyLength = 8;

    public static CustomOfficeCsv Parse(string text) {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture) {
            HasHeaderRecord = false,
            DetectDelimiter = true,
            BadDataFound = null,
            MissingFieldFound = null
        };
        using var csv = new CsvReader(new StringReader(text), config);
        if (!csv.Read())
            return Failure(I18nKey.Error.SupportSignatures.OfficeImport.Empty);

        var headers = (csv.Parser.Record ?? [])
            .Select(h => h.Trim())
            .ToList();
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++) {
            index.TryAdd(headers[i], i);
        }
        if (new[] { KeyColumn, NameColumn, StreetColumn, PostCodeColumn, CityColumn }.Any(c => !index.ContainsKey(c)))
            return Failure(I18nKey.Error.SupportSignatures.OfficeImport.MissingColumns);

        var rows = new List<CustomOfficeRow>();
        var problems = new List<OfficeImportProblemDTO>();
        var seenKeys = new HashSet<string>();
        var rowCount = 0;
        while (csv.Read()) {
            var record = csv.Parser.Record ?? [];
            if (record.All(string.IsNullOrWhiteSpace))
                continue;

            rowCount++;
            var line = csv.Parser.Row;
            var row = new CustomOfficeRow(line,
                Cell(record, index, KeyColumn).PadLeft(KeyLength, '0'),
                Cell(record, index, MunicipalityColumn),
                Cell(record, index, NameColumn),
                NullIfEmpty(Cell(record, index, AdditionColumn)),
                Cell(record, index, StreetColumn),
                Cell(record, index, PostCodeColumn),
                Cell(record, index, CityColumn));
            var problem = Problem(row);
            if (problem == null && !seenKeys.Add(row.MunicipalityKey))
                problem = I18nKey.Error.SupportSignatures.OfficeImport.DuplicateKey;
            if (problem != null) {
                problems.Add(new OfficeImportProblemDTO { Line = line, ErrorKey = problem });
                continue;
            }

            rows.Add(row);
        }

        return new CustomOfficeCsv(rows, problems, rowCount, rowCount == 0 ? I18nKey.Error.SupportSignatures.OfficeImport.Empty : null);
    }

    private static string? Problem(CustomOfficeRow row) {
        if (row.MunicipalityKey.Length != KeyLength || !row.MunicipalityKey.All(char.IsAsciiDigit))
            return I18nKey.Error.SupportSignatures.OfficeImport.InvalidKey;
        if (new[] { row.Name, row.Street, row.PostCode, row.City }.Any(v => v.Length == 0))
            return I18nKey.Error.SupportSignatures.OfficeImport.MissingField;
        if (new[] { row.Name, row.Addition ?? "", row.Street, row.PostCode, row.City }.Any(v => v.Length > CertificationOffice.AddressFieldMaxLength))
            return I18nKey.Error.SupportSignatures.OfficeImport.FieldTooLong;

        return null;
    }

    private static string Cell(string[] record, Dictionary<string, int> index, string column) {
        if (!index.TryGetValue(column, out var i) || i >= record.Length)
            return "";

        return record[i].Trim();
    }

    private static string? NullIfEmpty(string value) {
        return value.Length == 0 ? null : value;
    }

    private static CustomOfficeCsv Failure(string errorKey) {
        return new CustomOfficeCsv([], [], 0, errorKey);
    }
}
