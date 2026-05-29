using LinqToDB;
using LinqToDB.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.AdministrativeDivisions;

public class AdministrativeDivisionRepository {
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
            q = q.Where(ad => ad.Name.Contains(query)
                || (ad.PostCodes != null && ad.PostCodes.Contains(query)));
        }

        var totalCount = q.Count();
        var items = q.OrderBy(ad => ad.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (items, totalCount);
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