using InterpolatedSql.Dapper;
using LinqToDB;
using LinqToDB.Data;
using Quartermaster.Data.Abstract;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.AdministrativeDivisions;

public class AdministrativeDivisionRepository : RepositoryBase<AdministrativeDivision> {
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

    public List<Guid> GetAncestorIds(Guid divisionId) {
        var ids = new List<Guid>();
        var current = Get(divisionId);
        while (current != null) {
            ids.Add(current.Id);
            if (current.ParentId == null || current.ParentId == current.Id)
                break;
            current = Get(current.ParentId.Value);
        }
        return ids;
    }

}