using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;
using LinqToDB.Data;
using Quartermaster.Data.AdministrativeDivisions;

namespace Quartermaster.Data.SupportSignatures;

/// <summary>
/// Makes the official offices match the directory file: offices are matched by their key and overwritten, all official
/// assignments are replaced, and offices the file no longer has are deleted unless shipments went to them.
/// </summary>
public static class OfficialOfficeSync {
    public static OfficialOfficeSyncResult Apply(DbContext context, IReadOnlyList<OfficialOfficeEntry> entries) {
        var municipalityIdByKey = context.AdministrativeDivisions
            .Where(d => d.Depth == AdministrativeDivisionRepository.MunicipalityDepth && d.AdminCode != null)
            .Select(d => new { d.Id, d.AdminCode })
            .ToList()
            .GroupBy(d => d.AdminCode!.PadLeft(8, '0'))
            .ToDictionary(g => g.Key, g => g.First().Id);

        using var tx = context.BeginTransaction();
        var existing = context.CertificationOffices
            .Where(o => o.OfficialKey != null)
            .ToList()
            .ToDictionary(o => o.OfficialKey!);

        var created = new List<CertificationOffice>();
        var updated = 0;
        var officeIdByKey = new Dictionary<string, Guid>();
        foreach (var entry in entries) {
            var office = ToOffice(entry);
            if (existing.TryGetValue(entry.Key, out var current)) {
                office.Id = current.Id;
                if (Differs(current, office)) {
                    Update(context, office);
                    updated++;
                }
            } else {
                created.Add(office);
            }
            officeIdByKey[entry.Key] = office.Id;
        }
        context.BulkCopy(created);

        var assignments = new Dictionary<Guid, OfficialOfficeMunicipality>();
        var unknown = 0;
        foreach (var entry in entries) {
            foreach (var municipalityKey in entry.MunicipalityKeys) {
                if (!municipalityIdByKey.TryGetValue(municipalityKey, out var municipalityId)) {
                    unknown++;
                    continue;
                }

                assignments.TryAdd(municipalityId, new OfficialOfficeMunicipality { AdministrativeDivisionId = municipalityId, OfficeId = officeIdByKey[entry.Key] });
            }
        }
        context.OfficialOfficeMunicipalities.Delete();
        context.BulkCopy(assignments.Values);

        var (removed, kept) = RemoveVanished(context, existing.Values.Where(o => !officeIdByKey.ContainsKey(o.OfficialKey!)).Select(o => o.Id).ToList());
        tx.Commit();
        return new OfficialOfficeSyncResult(created.Count, updated, removed, kept, assignments.Count, unknown);
    }

    private static CertificationOffice ToOffice(OfficialOfficeEntry entry) {
        return new CertificationOffice {
            OfficialKey = entry.Key,
            Name = Fit(entry.Name, CertificationOffice.NameMaxLength),
            Street = Fit(entry.Street, CertificationOffice.AddressFieldMaxLength),
            PostCode = Fit(entry.PostCode, CertificationOffice.AddressFieldMaxLength),
            City = Fit(entry.City, CertificationOffice.AddressFieldMaxLength),
            Email = string.IsNullOrWhiteSpace(entry.Email) ? null : Fit(entry.Email, CertificationOffice.EmailMaxLength)
        };
    }

    private static string Fit(string value, int maxLength) {
        var trimmed = value.Trim();
        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }

    private static bool Differs(CertificationOffice current, CertificationOffice office) {
        if (current.Name != office.Name || current.Street != office.Street)
            return true;
        if (current.PostCode != office.PostCode || current.City != office.City)
            return true;

        return current.Email != office.Email;
    }

    private static void Update(DbContext context, CertificationOffice office) {
        context.CertificationOffices
            .Where(o => o.Id == office.Id)
            .Set(o => o.Name, office.Name)
            .Set(o => o.Street, office.Street)
            .Set(o => o.PostCode, office.PostCode)
            .Set(o => o.City, office.City)
            .Set(o => o.Email, office.Email)
            .Update();
    }

    private static (int Removed, int Kept) RemoveVanished(DbContext context, List<Guid> officeIds) {
        if (officeIds.Count == 0)
            return (0, 0);

        var withShipments = context.SignatureShipments
            .Where(s => officeIds.Contains(s.OfficeId))
            .Select(s => s.OfficeId)
            .Distinct()
            .ToList();
        var deletable = officeIds.Except(withShipments).ToList();
        context.CertificationOffices
            .Where(o => deletable.Contains(o.Id))
            .Delete();
        return (deletable.Count, withShipments.Count);
    }
}
