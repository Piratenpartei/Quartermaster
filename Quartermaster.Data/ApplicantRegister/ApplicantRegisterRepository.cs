using LinqToDB;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Data.AuditLog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.ApplicantRegister;

public class ApplicantRegisterRepository {
    private const string EntryAuditType = "ApplicantRegisterEntry";
    internal const string HitAuditType = "ApplicationRegisterHit";

    private readonly DbContext _context;
    private readonly AuditLogRepository _auditLog;

    public ApplicantRegisterRepository(DbContext context, AuditLogRepository auditLog) {
        _context = context;
        _auditLog = auditLog;
    }

    public List<ApplicantRegisterEntry> All()
        => _context.ApplicantRegisterEntries.OrderByDescending(e => e.OccurredOn).ToList();

    public (List<ApplicantRegisterEntry> Items, int TotalCount) Search(string? query, int page, int pageSize) {
        var q = _context.ApplicantRegisterEntries.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query)) {
            var term = query.Trim();
            q = q.Where(e => e.FirstName.Contains(term) || e.LastName.Contains(term));
        }

        var totalCount = q.Count();
        var items = q.OrderByDescending(e => e.OccurredOn)
            .ThenBy(e => e.LastName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (items, totalCount);
    }

    public ApplicantRegisterEntry? Get(Guid id)
        => _context.ApplicantRegisterEntries.Where(e => e.Id == id).FirstOrDefault();

    public Dictionary<Guid, ApplicantRegisterEntry> GetMany(IReadOnlyCollection<Guid> ids)
        => _context.ApplicantRegisterEntries
            .Where(e => ids.Contains(e.Id))
            .ToDictionary(e => e.Id);

    public void Create(ApplicantRegisterEntry entry) {
        _context.Insert(entry);
        _auditLog.LogCreated(EntryAuditType, entry.Id);
    }

    public void Update(ApplicantRegisterEntry entry) {
        using var tx = _context.BeginTransaction();
        var existing = Get(entry.Id);
        if (existing == null)
            return;

        _context.ApplicantRegisterEntries
            .Where(e => e.Id == entry.Id)
            .Set(e => e.Kind, entry.Kind)
            .Set(e => e.FirstName, entry.FirstName)
            .Set(e => e.LastName, entry.LastName)
            .Set(e => e.DateOfBirth, entry.DateOfBirth)
            .Set(e => e.Street, entry.Street)
            .Set(e => e.HouseNumber, entry.HouseNumber)
            .Set(e => e.PostCode, entry.PostCode)
            .Set(e => e.City, entry.City)
            .Set(e => e.Email, entry.Email)
            .Set(e => e.OccurredOn, entry.OccurredOn)
            .Set(e => e.Note, entry.Note)
            .Set(e => e.ChapterId, entry.ChapterId)
            .Update();
        LogChange(entry.Id, nameof(entry.Kind), existing.Kind.ToString(), entry.Kind.ToString());
        LogChange(entry.Id, nameof(entry.FirstName), existing.FirstName, entry.FirstName);
        LogChange(entry.Id, nameof(entry.LastName), existing.LastName, entry.LastName);
        LogChange(entry.Id, nameof(entry.DateOfBirth), existing.DateOfBirth?.ToString("O"), entry.DateOfBirth?.ToString("O"));
        LogChange(entry.Id, nameof(entry.Street), existing.Street, entry.Street);
        LogChange(entry.Id, nameof(entry.HouseNumber), existing.HouseNumber, entry.HouseNumber);
        LogChange(entry.Id, nameof(entry.PostCode), existing.PostCode, entry.PostCode);
        LogChange(entry.Id, nameof(entry.City), existing.City, entry.City);
        LogChange(entry.Id, nameof(entry.Email), existing.Email, entry.Email);
        LogChange(entry.Id, nameof(entry.OccurredOn), existing.OccurredOn.ToString("O"), entry.OccurredOn.ToString("O"));
        LogChange(entry.Id, nameof(entry.Note), existing.Note, entry.Note);
        LogChange(entry.Id, nameof(entry.ChapterId), existing.ChapterId?.ToString(), entry.ChapterId?.ToString());
        tx.Commit();
    }

    public void Delete(Guid id) {
        using var tx = _context.BeginTransaction();
        var hits = _context.ApplicationRegisterHits.Where(h => h.EntryId == id).ToList();
        foreach (var hit in hits)
            _auditLog.LogDeleted(HitAuditType, hit.Id);
        _context.ApplicationRegisterHits.Where(h => h.EntryId == id).Delete();
        _context.ApplicantRegisterEntries.Where(e => e.Id == id).Delete();
        _auditLog.LogDeleted(EntryAuditType, id);
        tx.Commit();
    }

    /// <summary>Runs inside the caller's transaction.</summary>
    public void AddHits(Guid applicationId, IReadOnlyList<ApplicationRegisterHit> hits) {
        if (hits.Count == 0)
            return;

        foreach (var hit in hits) {
            hit.ApplicationId = applicationId;
            _context.Insert(hit);
            _auditLog.LogCreated(HitAuditType, hit.Id);
        }
    }

    public List<ApplicationRegisterHit> HitsFor(Guid applicationId)
        => _context.ApplicationRegisterHits
            .Where(h => h.ApplicationId == applicationId)
            .OrderBy(h => h.CreatedAt)
            .ToList();

    /// <summary>False when the hit was already dismissed. Runs inside the caller's transaction.</summary>
    public bool Dismiss(Guid hitId, Guid userId, DateTime nowUtc) {
        var updated = _context.ApplicationRegisterHits
            .Where(h => h.Id == hitId && h.DismissedAt == null)
            .Set(h => h.DismissedAt, (DateTime?)nowUtc)
            .Set(h => h.DismissedByUserId, (Guid?)userId)
            .Update();
        if (updated == 0)
            return false;

        _auditLog.LogFieldChange(HitAuditType, hitId, nameof(ApplicationRegisterHit.DismissedAt), null, nowUtc.ToString("O"));
        return true;
    }

    public bool HasEntryFromApplication(Guid applicationId)
        => _context.ApplicantRegisterEntries.Any(e => e.SourceApplicationId == applicationId);

    public bool HasOpenHits(Guid applicationId)
        => _context.ApplicationRegisterHits.Any(h => h.ApplicationId == applicationId && h.DismissedAt == null);

    /// <summary>True while an undecided application has a hit on the entry that nobody dismissed; deleting the entry would leave that application
    /// needing the federal approval without a visible reason.</summary>
    public bool HasOpenHitsOfUndecidedApplications(Guid entryId)
        => _context.ApplicationRegisterHits
            .Where(h => h.EntryId == entryId && h.DismissedAt == null)
            .InnerJoin(_context.MembershipApplications, (h, a) => h.ApplicationId == a.Id, (h, a) => a)
            .Any(a => a.Status == ApplicationStatus.Pending
                || a.Status == ApplicationStatus.PendingDivisionLinking
                || a.Status == ApplicationStatus.PendingFederalApproval);

    private void LogChange(Guid entryId, string field, string? oldValue, string? newValue) {
        if (oldValue == newValue)
            return;
        _auditLog.LogFieldChange(EntryAuditType, entryId, field, oldValue, newValue);
    }
}
