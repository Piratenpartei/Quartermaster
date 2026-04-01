using LinqToDB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.MembershipApplications;

public class MembershipApplicationRepository {
    private readonly DbContext _context;

    public MembershipApplicationRepository(DbContext context) {
        _context = context;
    }

    public MembershipApplication? Get(Guid id)
        => _context.MembershipApplications.Where(a => a.Id == id && a.DeletedAt == null).FirstOrDefault();

    public void Create(MembershipApplication application) => _context.Insert(application);

    public (List<MembershipApplication> Items, int TotalCount) List(
        List<Guid> chapterIds, ApplicationStatus? status, int page, int pageSize) {

        var q = _context.MembershipApplications.Where(a => a.DeletedAt == null).AsQueryable();

        if (chapterIds.Count > 0)
            q = q.Where(a => a.ChapterId != null && chapterIds.Contains(a.ChapterId.Value));

        if (status != null)
            q = q.Where(a => a.Status == status.Value);

        var totalCount = q.Count();
        var items = q.OrderByDescending(a => a.SubmittedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (items, totalCount);
    }

    public void UpdateStatus(Guid id, ApplicationStatus status, Guid? processedByUserId) {
        _context.MembershipApplications
            .Where(a => a.Id == id)
            .Set(a => a.Status, status)
            .Set(a => a.ProcessedByUserId, processedByUserId)
            .Set(a => a.ProcessedAt, DateTime.UtcNow)
            .Update();
    }

    public void SoftDelete(Guid id) {
        _context.MembershipApplications.Where(x => x.Id == id).Set(x => x.DeletedAt, DateTime.UtcNow).Update();
    }
}
