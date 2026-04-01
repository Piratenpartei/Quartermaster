using LinqToDB;
using Quartermaster.Data.Abstract;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.DueSelector;

public class DueSelectionRepository : RepositoryBase<DueSelection> {
    private readonly DbContext _context;

    public DueSelectionRepository(DbContext context) {
        _context = context;
    }

    public DueSelection? Get(Guid id)
        => _context.DueSelections.Where(d => d.Id == id && d.DeletedAt == null).FirstOrDefault();

    public void Create(DueSelection selection) => _context.Insert(selection);

    public (List<DueSelection> Items, int TotalCount) List(
        DueSelectionStatus? status, int page, int pageSize) {

        var q = _context.DueSelections.Where(d => d.DeletedAt == null).AsQueryable();

        if (status != null)
            q = q.Where(d => d.Status == status.Value);

        var totalCount = q.Count();
        var items = q.OrderByDescending(d => d.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (items, totalCount);
    }

    public void UpdateStatus(Guid id, DueSelectionStatus status, Guid? processedByUserId) {
        _context.DueSelections
            .Where(d => d.Id == id)
            .Set(d => d.Status, status)
            .Set(d => d.ProcessedByUserId, processedByUserId)
            .Set(d => d.ProcessedAt, DateTime.UtcNow)
            .Update();
    }

    public void SoftDelete(Guid id) {
        _context.DueSelections.Where(x => x.Id == id).Set(x => x.DeletedAt, DateTime.UtcNow).Update();
    }
}
