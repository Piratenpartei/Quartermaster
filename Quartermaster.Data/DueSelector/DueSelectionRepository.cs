using LinqToDB;
using Quartermaster.Data.Abstract;
using Quartermaster.Data.AuditLog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.DueSelector;

public class DueSelectionRepository : RepositoryBase<DueSelection> {
    private readonly DbContext _context;
    private readonly AuditLogRepository _auditLog;

    public DueSelectionRepository(DbContext context, AuditLogRepository auditLog) {
        _context = context;
        _auditLog = auditLog;
    }

    public DueSelection? Get(Guid id)
        => _context.DueSelections.Where(d => d.Id == id && d.DeletedAt == null).FirstOrDefault();

    public void Create(DueSelection selection) {
        _context.Insert(selection);
        _auditLog.LogCreated("DueSelection", selection.Id);
    }

    public (List<DueSelection> Items, int TotalCount) List(
        DueSelectionStatus? status, int page, int pageSize,
        List<Guid>? allowedChapterIds = null) {

        var q = _context.DueSelections.Where(d => d.DeletedAt == null).AsQueryable();

        if (status != null)
            q = q.Where(d => d.Status == status.Value);

        if (allowedChapterIds != null) {
            var applicationDueSelectionIds = _context.MembershipApplications
                .Where(a => a.DeletedAt == null
                    && a.DueSelectionId != null
                    && a.ChapterId != null
                    && allowedChapterIds.Contains(a.ChapterId.Value))
                .Select(a => a.DueSelectionId!.Value)
                .ToList();
            q = q.Where(d => applicationDueSelectionIds.Contains(d.Id));
        }

        var totalCount = q.Count();
        var items = q.OrderByDescending(d => d.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (items, totalCount);
    }

    public void UpdateStatus(Guid id, DueSelectionStatus status, Guid? processedByUserId) {
        var existing = Get(id);
        _context.DueSelections
            .Where(d => d.Id == id)
            .Set(d => d.Status, status)
            .Set(d => d.ProcessedByUserId, processedByUserId)
            .Set(d => d.ProcessedAt, DateTime.UtcNow)
            .Update();
        if (existing != null)
            _auditLog.LogFieldChange("DueSelection", id, "Status", existing.Status.ToString(), status.ToString());
    }

    public void SoftDelete(Guid id) {
        _context.DueSelections.Where(x => x.Id == id).Set(x => x.DeletedAt, DateTime.UtcNow).Update();
        _auditLog.LogSoftDeleted("DueSelection", id);
    }
}
