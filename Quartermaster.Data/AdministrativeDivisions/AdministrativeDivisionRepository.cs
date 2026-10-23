using LinqToDB;
using LinqToDB.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.AdministrativeDivisions;

public class AdministrativeDivisionRepository {
    /// <summary>Depth of municipalities (Gemeinden) in the division tree built from GeoNames.</summary>
    public const int MunicipalityDepth = 7;

    private readonly DbContext _context;

    public AdministrativeDivisionRepository(DbContext context) {
        _context = context;
    }

    public AdministrativeDivision? Get(Guid id)
        => _context.AdministrativeDivisions.Where(ad => ad.Id == id).FirstOrDefault();

    public void Create(AdministrativeDivision division) => _context.Insert(division);

    public void CreateBulk(List<AdministrativeDivision> divisions) => _context.BulkCopy(divisions);

    public void SupplementDefaults() {
        if (Get(Guid.Empty) == null) {
            Create(new AdministrativeDivision {
                Id = Guid.Empty,
                Depth = 0,
                Name = "Null Island"
            });
        }
    }

    /// <summary>
    /// Stores the Gemeindeverbände and links every municipality (depth 7) to its one by the official key; our <c>AdminCode</c> is that
    /// key without its leading zero. Returns how many municipalities found no entry in the file.
    /// </summary>
    public int ApplyAssociations(Gv100AdData data, DateTime importedAt) {
        foreach (var association in data.Associations) {
            _context.InsertOrReplace(new MunicipalityAssociation {
                Key = association.Key,
                Name = association.Name,
                Kind = association.Kind,
                ImportedAt = importedAt
            });
        }

        var keyByAgs = data.Municipalities
            .GroupBy(m => m.Ags)
            .ToDictionary(g => g.Key, g => g.First().AssociationKey);
        var municipalities = _context.AdministrativeDivisions
            .Where(d => d.Depth == MunicipalityDepth && d.AdminCode != null)
            .Select(d => new { d.Id, d.AdminCode, d.MunicipalityAssociationKey })
            .ToList();

        var unmatched = 0;
        var idsByNewKey = new Dictionary<string, List<Guid>>();
        var idsToClear = new List<Guid>();
        foreach (var municipality in municipalities) {
            var found = keyByAgs.TryGetValue(municipality.AdminCode!.PadLeft(8, '0'), out var key);
            if (!found)
                unmatched++;
            if (key == municipality.MunicipalityAssociationKey)
                continue;

            if (key == null) {
                idsToClear.Add(municipality.Id);
            } else {
                if (!idsByNewKey.TryGetValue(key, out var ids))
                    idsByNewKey[key] = ids = [];
                ids.Add(municipality.Id);
            }
        }

        foreach (var (key, ids) in idsByNewKey) {
            _context.AdministrativeDivisions
                .Where(d => ids.Contains(d.Id))
                .Set(d => d.MunicipalityAssociationKey, key)
                .Update();
        }
        if (idsToClear.Count > 0) {
            _context.AdministrativeDivisions
                .Where(d => idsToClear.Contains(d.Id))
                .Set(d => d.MunicipalityAssociationKey, (string?)null)
                .Update();
        }

        return unmatched;
    }

    public List<AdministrativeDivision> GetMany(IEnumerable<Guid> ids) {
        var idList = ids.Distinct().ToList();
        return _context.AdministrativeDivisions
            .Where(d => idList.Contains(d.Id))
            .ToList();
    }

    /// <summary>Municipalities by their official key (AGS, 8 digits); our admin codes may lack the leading zero.</summary>
    public Dictionary<string, AdministrativeDivision> GetMunicipalitiesByKey(IEnumerable<string> keys) {
        var padded = keys.Distinct().ToList();
        var codes = padded
            .Concat(padded.Select(k => k.TrimStart('0')))
            .Distinct()
            .ToList();
        return _context.AdministrativeDivisions
            .Where(d => d.Depth == MunicipalityDepth && codes.Contains(d.AdminCode!))
            .ToList()
            .GroupBy(d => d.AdminCode!.PadLeft(8, '0'))
            .ToDictionary(g => g.Key, g => g.First());
    }

    public MunicipalityAssociation? GetAssociation(string key) {
        return _context.MunicipalityAssociations.FirstOrDefault(a => a.Key == key);
    }

    public List<AdministrativeDivision> GetMunicipalitiesOfAssociation(string key) {
        return _context.AdministrativeDivisions
            .Where(d => d.Depth == MunicipalityDepth && d.MunicipalityAssociationKey == key)
            .OrderBy(d => d.Name)
            .ToList();
    }

    public List<AdministrativeDivision> GetRoots()
        => _context.AdministrativeDivisions.Where(ad => ad.Depth == 1).OrderBy(ad => ad.Name).ToList();

    public List<AdministrativeDivision> GetChildren(Guid parentId)
        => _context.AdministrativeDivisions.Where(ad => ad.ParentId == parentId && ad.Id != parentId).OrderBy(ad => ad.Name).ToList();

    /// <summary>Children of several divisions in one query — for batch resolution of representative data.</summary>
    public List<AdministrativeDivision> GetChildrenForParents(List<Guid> parentIds)
        => parentIds.Count == 0
            ? new List<AdministrativeDivision>()
            : _context.AdministrativeDivisions
                .Where(ad => ad.ParentId != null && parentIds.Contains(ad.ParentId.Value) && ad.Id != ad.ParentId)
                .ToList();

    public (List<AdministrativeDivision> Items, int TotalCount) Search(string? query, int page, int pageSize) {
        var q = _context.AdministrativeDivisions.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query)) {
            var bounded = "," + query + ",";
            q = q.Where(ad => ad.Name.Contains(query)
                || (ad.PostCodes != null && ("," + ad.PostCodes + ",").Contains(bounded)));
        }

        var totalCount = q.Count();
        var items = q.OrderBy(ad => ad.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (items, totalCount);
    }

    /// <summary>Returns every division whose comma-separated <see cref="AdministrativeDivision.PostCodes"/> contains the given code as a whole entry.</summary>
    public List<AdministrativeDivision> FindByPostCode(string postCode) {
        if (string.IsNullOrWhiteSpace(postCode))
            return new List<AdministrativeDivision>();

        var bounded = "," + postCode + ",";
        return _context.AdministrativeDivisions
            .Where(ad => ad.PostCodes != null && ("," + ad.PostCodes + ",").Contains(bounded))
            .ToList();
    }

    public (List<AdminDivisionImportLog> Items, int TotalCount) GetImportHistory(int page, int pageSize) {
        var q = _context.AdminDivisionImportLogs.AsQueryable();
        var totalCount = q.Count();
        var items = q.OrderByDescending(l => l.ImportedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        return (items, totalCount);
    }

    private class DivisionTreeRow {
        public Guid Id { get; set; }
        public Guid? ParentId { get; set; }
        public int Depth { get; set; }
    }

    public List<Guid> GetDescendantIds(Guid divisionId) {
        var cte = _context.GetCte<DivisionTreeRow>(self =>
            _context.AdministrativeDivisions
                .Where(d => d.Id == divisionId)
                .Select(d => new DivisionTreeRow { Id = d.Id, ParentId = d.ParentId, Depth = 0 })
                .Concat(self.SelectMany(prev => _context.AdministrativeDivisions
                    .InnerJoin(d => d.ParentId == prev.Id && d.Id != prev.Id)
                    .Select(d => new DivisionTreeRow { Id = d.Id, ParentId = d.ParentId, Depth = prev.Depth + 1 }))));

        return cte.OrderBy(r => r.Depth).Select(r => r.Id).ToList();
    }

    public List<Guid> GetAncestorIds(Guid divisionId) {
        var cte = _context.GetCte<DivisionTreeRow>(self =>
            _context.AdministrativeDivisions
                .Where(d => d.Id == divisionId)
                .Select(d => new DivisionTreeRow { Id = d.Id, ParentId = d.ParentId, Depth = 0 })
                .Concat(self.SelectMany(prev => _context.AdministrativeDivisions
                    .InnerJoin(d => d.Id == prev.ParentId && d.Id != prev.Id)
                    .Select(d => new DivisionTreeRow { Id = d.Id, ParentId = d.ParentId, Depth = prev.Depth + 1 }))));

        return cte.OrderBy(r => r.Depth).Select(r => r.Id).ToList();
    }
}