using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures;

/// <summary>
/// Imports hand-kept office addresses. A row whose address matches the official one and has no addition stores nothing and drops an
/// earlier hand-entered office of that municipality; every other row moves its municipality to the hand-entered office with that
/// name, addition and address, which is created when missing. The file wins over earlier assignments.
/// </summary>
public class CustomOfficeImporter {
    private readonly CertificationOfficeRepository _offices;
    private readonly AdministrativeDivisionRepository _divisions;
    private readonly AuditLogRepository _auditLog;

    public CustomOfficeImporter(CertificationOfficeRepository offices, AdministrativeDivisionRepository divisions, AuditLogRepository auditLog) {
        _offices = offices;
        _divisions = divisions;
        _auditLog = auditLog;
    }

    public (OfficeImportResultDTO? Result, string? ErrorKey) Run(byte[] content, bool apply) {
        var csv = CustomOfficeCsvParser.Parse(CsvTextDecoder.Decode(content));
        if (csv.ErrorKey != null)
            return (null, csv.ErrorKey);

        var result = new OfficeImportResultDTO { Rows = csv.RowCount, Problems = [.. csv.Problems] };
        var municipalities = _divisions.GetMunicipalitiesByKey(csv.Rows.Select(r => r.MunicipalityKey));
        result.UnknownKeys = csv.Rows
            .Where(r => !municipalities.ContainsKey(r.MunicipalityKey))
            .Select(r => r.MunicipalityName.Length == 0 ? r.MunicipalityKey : $"{r.MunicipalityName} ({r.MunicipalityKey})")
            .ToList();
        var known = csv.Rows
            .Where(r => municipalities.ContainsKey(r.MunicipalityKey))
            .ToList();

        var ids = known.Select(r => municipalities[r.MunicipalityKey].Id).ToList();
        var officialByMunicipality = _offices.GetOfficialOfficeIdsByMunicipality(ids);
        var officialOffices = _offices.GetMany(officialByMunicipality.Values).ToDictionary(o => o.Id);
        var customByMunicipality = _offices.GetCustomOfficeIdsByMunicipality(ids);

        var sameAsOfficial = new List<Guid>();
        var groups = new Dictionary<string, List<CustomOfficeRow>>();
        foreach (var row in known) {
            var municipalityId = municipalities[row.MunicipalityKey].Id;
            var official = officialByMunicipality.TryGetValue(municipalityId, out var officialId) ? officialOffices.GetValueOrDefault(officialId) : null;
            if (MatchesOfficial(row, official)) {
                sameAsOfficial.Add(municipalityId);
                continue;
            }

            var key = OfficeAddressComparer.OfficeKey(row.Name, row.Addition, row.Street, row.PostCode, row.City);
            if (!groups.TryGetValue(key, out var rows))
                groups[key] = rows = [];
            rows.Add(row);
        }
        result.SameAsOfficial = sameAsOfficial.Count;
        result.RemovedAssignments = sameAsOfficial.Count(customByMunicipality.ContainsKey);

        var customByKey = _offices.ListCustom()
            .GroupBy(o => OfficeAddressComparer.OfficeKey(o.Name, o.AddressAddition, o.Street, o.PostCode, o.City))
            .ToDictionary(g => g.Key, g => g.First());
        result.Entries = groups
            .Select(g => Entry(g.Value, customByKey.GetValueOrDefault(g.Key), municipalities))
            .OrderBy(e => e.Name)
            .ToList();
        if (!apply)
            return (result, null);

        using var tx = _offices.BeginTransaction();
        _offices.RemoveAssignments(sameAsOfficial);
        var targets = new HashSet<Guid>();
        foreach (var (key, rows) in groups) {
            var office = customByKey.GetValueOrDefault(key) ?? Create(rows[0]);
            targets.Add(office.Id);
            _offices.Assign(office.Id, rows.Select(r => municipalities[r.MunicipalityKey].Id));
        }
        DeleteEmptied(customByMunicipality.Values.Where(id => !targets.Contains(id)));
        tx.Commit();

        result.Applied = true;
        return (result, null);
    }

    private static bool MatchesOfficial(CustomOfficeRow row, CertificationOffice? official) {
        if (official == null || row.Addition != null)
            return false;

        return OfficeAddressComparer.SameAddress(row.Street, row.PostCode, row.City, official.Street, official.PostCode, official.City);
    }

    private static OfficeImportEntryDTO Entry(List<CustomOfficeRow> rows, CertificationOffice? existing, Dictionary<string, AdministrativeDivision> municipalities) {
        var first = rows[0];
        return new OfficeImportEntryDTO {
            Name = existing?.Name ?? first.Name,
            AddressAddition = existing == null ? first.Addition : existing.AddressAddition,
            Street = existing?.Street ?? first.Street,
            PostCode = existing?.PostCode ?? first.PostCode,
            City = existing?.City ?? first.City,
            IsNew = existing == null,
            Municipalities = rows
                .Select(r => municipalities[r.MunicipalityKey].Name)
                .OrderBy(n => n)
                .ToList()
        };
    }

    private CertificationOffice Create(CustomOfficeRow row) {
        var office = new CertificationOffice {
            Name = row.Name,
            AddressAddition = row.Addition,
            Street = row.Street,
            PostCode = row.PostCode,
            City = row.City
        };
        _offices.Create(office, []);
        _auditLog.LogCreated("CertificationOffice", office.Id);
        return office;
    }

    /// <summary>Deletes the hand-entered offices the import took every municipality from, unless shipments went to them.</summary>
    private void DeleteEmptied(IEnumerable<Guid> officeIds) {
        foreach (var officeId in officeIds.Distinct()) {
            if (_offices.GetMunicipalityIds(officeId).Count > 0 || _offices.HasShipments(officeId))
                continue;

            _offices.Delete(officeId);
            _auditLog.Log("CertificationOffice", officeId, "Deleted");
        }
    }
}
