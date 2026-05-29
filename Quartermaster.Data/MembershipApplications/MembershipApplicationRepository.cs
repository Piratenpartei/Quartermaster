using LinqToDB;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Data.AuditLog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.MembershipApplications;

public class MembershipApplicationRepository {
    private readonly DbContext _context;
    private readonly AuditLogRepository _auditLog;

    public MembershipApplicationRepository(DbContext context, AuditLogRepository auditLog) {
        _context = context;
        _auditLog = auditLog;
    }

    public MembershipApplication? Get(Guid id)
        => _context.MembershipApplications.Where(a => a.Id == id && a.DeletedAt == null).FirstOrDefault();

    public MembershipApplication? GetByDueSelectionId(Guid dueSelectionId)
        => _context.MembershipApplications
            .Where(a => a.DueSelectionId == dueSelectionId && a.DeletedAt == null)
            .FirstOrDefault();

    public void Create(MembershipApplication application) {
        _context.Insert(application);
        _auditLog.LogCreated("MembershipApplication", application.Id);
    }

    /// <summary>
    /// Lists applications. <paramref name="chapterIds"/> contract:
    /// <c>null</c> = no chapter filter (only callers with global view should pass this);
    /// non-null list = exactly these chapters (an empty list returns zero rows, never widens).
    /// </summary>
    public (List<MembershipApplication> Items, int TotalCount) List(
        List<Guid>? chapterIds, ApplicationStatus? status, int page, int pageSize) {

        var q = _context.MembershipApplications.Where(a => a.DeletedAt == null).AsQueryable();

        if (chapterIds != null)
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
        using var tx = _context.BeginTransaction();
        var existing = Get(id);
        _context.MembershipApplications
            .Where(a => a.Id == id)
            .Set(a => a.Status, status)
            .Set(a => a.ProcessedByUserId, processedByUserId)
            .Set(a => a.ProcessedAt, DateTime.UtcNow)
            .Update();
        if (existing != null)
            _auditLog.LogFieldChange("MembershipApplication", id, "Status", existing.Status.ToString(), status.ToString());
        tx.Commit();
    }

    /// <summary>
    /// Assigns the administrative division + chapter to an application that was waiting in
    /// PendingDivisionLinking and moves it to Pending so normal review can begin.
    /// </summary>
    public void LinkDivisionAndChapter(Guid id, Guid? divisionId, Guid? chapterId) {
        using var tx = _context.BeginTransaction();
        var existing = Get(id);
        _context.MembershipApplications
            .Where(a => a.Id == id)
            .Set(a => a.AddressAdministrativeDivisionId, divisionId)
            .Set(a => a.ChapterId, chapterId)
            .Set(a => a.Status, ApplicationStatus.Pending)
            .Update();
        if (existing != null)
            _auditLog.LogFieldChange("MembershipApplication", id, "ChapterId", existing.ChapterId?.ToString(), chapterId?.ToString());
        tx.Commit();
    }

    public void SetMemberNumberAndWelcome(Guid id, int memberNumber, DateTime sentAt) {
        using var tx = _context.BeginTransaction();
        _context.MembershipApplications
            .Where(a => a.Id == id)
            .Set(a => a.MemberNumber, (int?)memberNumber)
            .Set(a => a.WelcomeSentAt, (DateTime?)sentAt)
            .Update();
        _auditLog.LogFieldChange("MembershipApplication", id, "MemberNumber", null, memberNumber.ToString());
        tx.Commit();
    }

    public void SoftDelete(Guid id) {
        _context.MembershipApplications.Where(x => x.Id == id).Set(x => x.DeletedAt, DateTime.UtcNow).Update();
        _auditLog.LogSoftDeleted("MembershipApplication", id);
    }

    /// <summary>Self-clock only (ProcessedAt or SubmittedAt + 11y). The linked-Member hybrid rule lives in <c>RetentionAnonymizationService</c>.</summary>
    public List<MembershipApplication> GetEligibleForAnonymization(DateTime now) {
        var thresholdYear = now.Year - 11;
        return _context.MembershipApplications
            .Where(a => a.AnonymizedAt == null
                && (a.ProcessedAt != null
                    ? a.ProcessedAt.Value.Year <= thresholdYear
                    : a.SubmittedAt.Year <= thresholdYear))
            .ToList();
    }

    /// <summary>Nulls PII fields; keeps name + DOB + structural/processing fields.</summary>
    public void Anonymize(Guid id) {
        var now = DateTime.UtcNow;
        _context.MembershipApplications.Where(a => a.Id == id)
            .Set(a => a.Email, "")
            .Set(a => a.PhoneNumber, "")
            .Set(a => a.AddressStreet, "")
            .Set(a => a.AddressHouseNbr, "")
            .Set(a => a.AddressPostCode, "")
            .Set(a => a.AddressCity, "")
            .Set(a => a.AddressAdministrativeDivisionId, (Guid?)null)
            .Set(a => a.Citizenship, "")
            .Set(a => a.ApplicationText, "")
            .Set(a => a.AnonymizedAt, (DateTime?)now)
            .Update();
        _auditLog.Log("MembershipApplication", id, "Anonymized");
    }
}
