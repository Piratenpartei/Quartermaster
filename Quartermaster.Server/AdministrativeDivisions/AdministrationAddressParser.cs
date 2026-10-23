using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.AdministrativeDivisions;

/// <summary>
/// Reads the "Anschriften" sheet of the Destatis directory "Anschriften der Gemeinde- und Stadtverwaltungen" saved as CSV. Columns
/// are found by their header, so the rows above it and a semicolon-separated export work too. Every municipality row (Satzart 60)
/// names the administration that serves it ("Verwaltungssitz", e.g. its Samtgemeinde) with that administration's address; rows with
/// the same administration in the same Kreis become one office.
/// A member of a Verwaltungsgemeinschaft or Verwaltungsverband can have two rows, one for the association (listed as Satzart 50) and
/// one for its own administration. Elections are a delegated task the association carries out, except in Baden-Württemberg, where
/// the Gemeinde itself certifies the voting rights; so the association row wins there only outside Baden-Württemberg.
/// </summary>
public static class AdministrationAddressParser {
    private const string AssociationRecordType = "50";
    private const string MunicipalityRecordType = "60";
    private const string BadenWuerttembergKey = "08";
    private const int KreisKeyLength = 5;
    private const int AssociationKeyLength = 9;
    private const int MunicipalityRegionalKeyLength = 12;
    private const int MunicipalityKeyLength = 8;

    public static AdministrationAddressData Parse(TextReader reader) {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture) {
            HasHeaderRecord = false,
            DetectDelimiter = true,
            BadDataFound = null,
            MissingFieldFound = null
        };
        using var csv = new CsvReader(reader, config);

        Columns? columns = null;
        var associations = new HashSet<string>();
        var rowsByMunicipality = new Dictionary<string, List<string[]>>();
        var order = new List<string>();
        while (csv.Read()) {
            var record = csv.Parser.Record ?? [];
            if (columns == null) {
                columns = Columns.FromHeader(record);
                continue;
            }

            var recordType = Cell(record, columns.RecordType);
            if (recordType == AssociationRecordType) {
                associations.Add(AssociationKey(Cell(record, columns.RegionalKey).PadLeft(AssociationKeyLength, '0'), Cell(record, columns.Administration)));
                continue;
            }
            if (recordType != MunicipalityRecordType)
                continue;

            var municipalityKey = Cell(record, columns.MunicipalityKey).PadLeft(MunicipalityKeyLength, '0');
            if (!rowsByMunicipality.TryGetValue(municipalityKey, out var rows)) {
                rowsByMunicipality[municipalityKey] = rows = [];
                order.Add(municipalityKey);
            }
            rows.Add(record);
        }

        if (columns == null)
            throw new InvalidDataException("No header row with the columns Satzart, ARS, AGS, Verwaltungssitz, Straße, PLZ and Ort found");

        var officeOrder = new List<string>();
        var firstRowByOffice = new Dictionary<string, string[]>();
        var keysByOffice = new Dictionary<string, List<string>>();
        var withoutAddress = 0;
        var twoAdministrations = 0;
        foreach (var municipalityKey in order) {
            var rows = rowsByMunicipality[municipalityKey];
            if (rows.Count > 1)
                twoAdministrations++;

            var record = Responsible(municipalityKey, rows, columns, associations);
            if (!HasAddress(record, columns)) {
                withoutAddress++;
                continue;
            }

            var officeKey = $"{municipalityKey[..KreisKeyLength]}|{Cell(record, columns.Administration)}";
            if (!keysByOffice.TryGetValue(officeKey, out var keys)) {
                keysByOffice[officeKey] = keys = [];
                firstRowByOffice[officeKey] = record;
                officeOrder.Add(officeKey);
            }
            keys.Add(municipalityKey);
        }

        var offices = officeOrder
            .Select(key => Entry(key, firstRowByOffice[key], columns, keysByOffice[key]))
            .ToList();
        return new AdministrationAddressData(offices, order.Count - withoutAddress, withoutAddress, twoAdministrations);
    }

    /// <summary>The row of the administration that certifies for the municipality when the directory lists two.</summary>
    private static string[] Responsible(string municipalityKey, List<string[]> rows, Columns columns, HashSet<string> associations) {
        if (rows.Count == 1)
            return rows[0];

        var preferAssociation = !municipalityKey.StartsWith(BadenWuerttembergKey, StringComparison.Ordinal);
        var match = rows.FirstOrDefault(r => IsAssociationRow(r, columns, associations) == preferAssociation);
        return match ?? rows[0];
    }

    private static bool IsAssociationRow(string[] record, Columns columns, HashSet<string> associations) {
        var regionalKey = Cell(record, columns.RegionalKey).PadLeft(MunicipalityRegionalKeyLength, '0');
        return associations.Contains(AssociationKey(regionalKey[..AssociationKeyLength], Cell(record, columns.Administration)));
    }

    private static string AssociationKey(string regionalKey, string name) {
        return $"{regionalKey}|{name}";
    }

    private static OfficialOfficeEntry Entry(string key, string[] record, Columns columns, List<string> municipalityKeys) {
        var email = columns.Email == null ? "" : Cell(record, columns.Email.Value);
        return new OfficialOfficeEntry(key, Cell(record, columns.Administration), Cell(record, columns.Street), Cell(record, columns.PostCode),
            Cell(record, columns.City), email.Length == 0 ? null : email, municipalityKeys);
    }

    private static bool HasAddress(string[] record, Columns columns) {
        if (Cell(record, columns.Administration).Length == 0)
            return false;
        if (Cell(record, columns.Street).Length == 0)
            return false;
        if (Cell(record, columns.PostCode).Length == 0)
            return false;

        return Cell(record, columns.City).Length > 0;
    }

    private static string Cell(string[] record, int index) {
        return index < record.Length ? record[index].Trim() : "";
    }

    private record Columns(int RecordType, int RegionalKey, int MunicipalityKey, int Administration, int Street, int PostCode, int City, int? Email) {
        /// <summary>The columns of the header row, or null when the record is one of the title rows above it.</summary>
        public static Columns? FromHeader(string[] record) {
            var headers = record.Select(h => h.Trim()).ToList();
            var recordType = headers.IndexOf("Satzart");
            if (recordType < 0)
                return null;

            var regionalKey = headers.FindIndex(h => h.Contains("(ARS)", StringComparison.Ordinal));
            var municipalityKey = headers.FindIndex(h => h.Contains("(AGS)", StringComparison.Ordinal));
            var administration = headers.IndexOf("Verwaltungssitz");
            var street = headers.IndexOf("Straße");
            var postCode = headers.IndexOf("PLZ");
            var city = headers.IndexOf("Ort");
            var email = headers.FindIndex(h => h.StartsWith("E-Mail", StringComparison.Ordinal));
            if (new[] { regionalKey, municipalityKey, administration, street, postCode, city }.Any(i => i < 0))
                return null;

            return new Columns(recordType, regionalKey, municipalityKey, administration, street, postCode, city, email < 0 ? null : email);
        }
    }
}
