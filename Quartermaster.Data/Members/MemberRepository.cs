using LinqToDB;
using Quartermaster.Data.AuditLog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.Members;

public class MemberRepository {
    private readonly DbContext _context;
    private readonly AuditLogRepository _auditLog;

    public MemberRepository(DbContext context, AuditLogRepository auditLog) {
        _context = context;
        _auditLog = auditLog;
    }

    public Member? Get(Guid id)
        => _context.Members.Where(m => m.Id == id).FirstOrDefault();

    public Member? GetByMemberNumber(int memberNumber)
        => _context.Members.Where(m => m.MemberNumber == memberNumber).FirstOrDefault();

    /// <summary>Lookup by AdmissionReference, scoped to floating members (those without a MemberNumber yet). Used by the import to upsert floating applicants and to detect a floating-to-active promotion.</summary>
    public Member? GetFloatingByAdmissionReference(string admissionReference)
        => _context.Members
            .Where(m => m.MemberNumber == null && m.AdmissionReference == admissionReference)
            .FirstOrDefault();

    public Member? GetByUserId(Guid userId)
        => _context.Members.Where(m => m.MemberNumber != null && m.UserId == userId).FirstOrDefault();

    /// <summary>Like <see cref="GetByUserId"/>, but also finds a floating member (accepted, no member number yet).</summary>
    public Member? GetByUserIdIncludingFloating(Guid userId)
        => _context.Members
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.MemberNumber == null)
            .FirstOrDefault();

    public void SetUserId(Guid memberId, Guid userId) {
        _context.Members
            .Where(m => m.Id == memberId)
            .Set(m => m.UserId, userId)
            .Update();
    }

    public void SetChapterId(Guid memberId, Guid? oldChapterId, Guid chapterId) {
        _context.Members
            .Where(m => m.Id == memberId)
            .Set(m => m.ChapterId, chapterId)
            .Update();
        _auditLog.LogFieldChange("Member", memberId, "ChapterId", oldChapterId?.ToString(), chapterId.ToString());
    }

    public void SetVotingRights(Guid memberId, bool oldValue, bool newValue) {
        _context.Members
            .Where(m => m.Id == memberId)
            .Set(m => m.HasVotingRights, newValue)
            .Update();
        _auditLog.LogFieldChange("Member", memberId, "HasVotingRights", oldValue.ToString(), newValue.ToString());
    }

    public List<Member> GetByChapterIds(List<Guid> chapterIds) {
        return _context.Members
            .Where(m => m.MemberNumber != null && m.ChapterId != null && chapterIds.Contains(m.ChapterId.Value))
            .ToList();
    }

    public List<Member> GetByAdministrativeDivisionId(Guid divisionId) {
        return _context.Members
            .Where(m => m.MemberNumber != null && m.ResidenceAdministrativeDivisionId == divisionId)
            .ToList();
    }

    public List<Member> GetByAdministrativeDivisionIds(List<Guid> divisionIds) {
        return _context.Members
            .Where(m => m.MemberNumber != null && m.ResidenceAdministrativeDivisionId != null
                && divisionIds.Contains(m.ResidenceAdministrativeDivisionId.Value))
            .ToList();
    }

    public (List<Member> Items, int TotalCount) Search(
        string? query, Guid? chapterId, int page, int pageSize,
        List<Guid>? allowedChapterIds = null, bool orphanedOnly = false, bool floatingOnly = false) {

        var q = _context.Members.AsQueryable();

        if (floatingOnly)
            q = q.Where(m => m.MemberNumber == null);

        if (!string.IsNullOrWhiteSpace(query)) {
            if (int.TryParse(query, out var memberNum)) {
                q = q.Where(m => m.MemberNumber == memberNum);
            } else {
                q = q.Where(m => m.FirstName.Contains(query)
                    || m.LastName.Contains(query)
                    || (m.Email != null && m.Email.Contains(query)));
            }
        }

        if (chapterId.HasValue)
            q = q.Where(m => m.ChapterId == chapterId.Value);

        if (allowedChapterIds != null)
            q = q.Where(m => m.ChapterId != null && allowedChapterIds.Contains(m.ChapterId.Value));

        if (orphanedOnly) {
            var orphanedIds = _context.AdministrativeDivisions
                .Where(d => d.IsOrphaned)
                .Select(d => d.Id)
                .ToList();
            q = q.Where(m => m.ResidenceAdministrativeDivisionId != null
                && orphanedIds.Contains(m.ResidenceAdministrativeDivisionId.Value));
        }

        var totalCount = q.Count();
        var items = q.OrderBy(m => m.LastName).ThenBy(m => m.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (items, totalCount);
    }

    public void Insert(Member member) {
        _context.Insert(member);
        _auditLog.LogCreated("Member", member.Id);
    }

    /// <summary>Returns true if a SQL write happened, false if all import fields matched the existing row and the update was skipped.
    /// LastImportedAt alone is not considered a material change because the importer always bumps it; treating that as "changed"
    /// would write a row + audit-log entry per member on every re-import even when the CSV content is identical.</summary>
    public bool Update(Member member) {
        var existing = _context.Members.Where(m => m.Id == member.Id).FirstOrDefault();

        if (existing != null && AreImportFieldsEqual(existing, member))
            return false;

        _context.Members
            .Where(m => m.Id == member.Id)
            .Set(m => m.MemberNumber, member.MemberNumber)
            .Set(m => m.AdmissionReference, member.AdmissionReference)
            .Set(m => m.FirstName, member.FirstName)
            .Set(m => m.LastName, member.LastName)
            .Set(m => m.Street, member.Street)
            .Set(m => m.Country, member.Country)
            .Set(m => m.PostCode, member.PostCode)
            .Set(m => m.City, member.City)
            .Set(m => m.Phone, member.Phone)
            .Set(m => m.Email, member.Email)
            .Set(m => m.DateOfBirth, member.DateOfBirth)
            .Set(m => m.Citizenship, member.Citizenship)
            .Set(m => m.MembershipFee, member.MembershipFee)
            .Set(m => m.ReducedFee, member.ReducedFee)
            .Set(m => m.FirstFee, member.FirstFee)
            .Set(m => m.OpenFeeTotal, member.OpenFeeTotal)
            .Set(m => m.ReducedFeeEnd, member.ReducedFeeEnd)
            .Set(m => m.EntryDate, member.EntryDate)
            .Set(m => m.ExitDate, member.ExitDate)
            .Set(m => m.FederalState, member.FederalState)
            .Set(m => m.County, member.County)
            .Set(m => m.Municipality, member.Municipality)
            .Set(m => m.IsPending, member.IsPending)
            .Set(m => m.HasVotingRights, member.HasVotingRights)
            .Set(m => m.ReceivesSurveys, member.ReceivesSurveys)
            .Set(m => m.ReceivesActions, member.ReceivesActions)
            .Set(m => m.ReceivesNewsletter, member.ReceivesNewsletter)
            .Set(m => m.PostBounce, member.PostBounce)
            .Set(m => m.ChapterId, member.ChapterId)
            .Set(m => m.ResidenceAdministrativeDivisionId, member.ResidenceAdministrativeDivisionId)
            .Set(m => m.IsImported, member.IsImported)
            .Set(m => m.LastImportedAt, member.LastImportedAt)
            .Update();

        if (existing != null)
            LogMemberFieldChanges(existing, member);
        return true;
    }

    /// <summary>Mirrors the column list in <see cref="Update"/>, intentionally omitting <c>LastImportedAt</c>.
    /// Keep these two in sync: a new column in Update belongs in this comparison as well.</summary>
    private static bool AreImportFieldsEqual(Member a, Member b)
        => a.MemberNumber == b.MemberNumber
        && a.AdmissionReference == b.AdmissionReference
        && a.FirstName == b.FirstName
        && a.LastName == b.LastName
        && a.Street == b.Street
        && a.Country == b.Country
        && a.PostCode == b.PostCode
        && a.City == b.City
        && a.Phone == b.Phone
        && a.Email == b.Email
        && a.DateOfBirth == b.DateOfBirth
        && a.Citizenship == b.Citizenship
        && a.MembershipFee == b.MembershipFee
        && a.ReducedFee == b.ReducedFee
        && a.FirstFee == b.FirstFee
        && a.OpenFeeTotal == b.OpenFeeTotal
        && a.ReducedFeeEnd == b.ReducedFeeEnd
        && a.EntryDate == b.EntryDate
        && a.ExitDate == b.ExitDate
        && a.FederalState == b.FederalState
        && a.County == b.County
        && a.Municipality == b.Municipality
        && a.IsPending == b.IsPending
        && a.HasVotingRights == b.HasVotingRights
        && a.ReceivesSurveys == b.ReceivesSurveys
        && a.ReceivesActions == b.ReceivesActions
        && a.ReceivesNewsletter == b.ReceivesNewsletter
        && a.PostBounce == b.PostBounce
        && a.ChapterId == b.ChapterId
        && a.ResidenceAdministrativeDivisionId == b.ResidenceAdministrativeDivisionId
        && a.IsImported == b.IsImported;

    private void LogMemberFieldChanges(Member old, Member updated) {
        _auditLog.LogFieldChange("Member", updated.Id, "MemberNumber", old.MemberNumber?.ToString(), updated.MemberNumber?.ToString());
        _auditLog.LogFieldChange("Member", updated.Id, "AdmissionReference", old.AdmissionReference, updated.AdmissionReference);
        _auditLog.LogFieldChange("Member", updated.Id, "FirstName", old.FirstName, updated.FirstName);
        _auditLog.LogFieldChange("Member", updated.Id, "LastName", old.LastName, updated.LastName);
        _auditLog.LogFieldChange("Member", updated.Id, "Street", old.Street, updated.Street);
        _auditLog.LogFieldChange("Member", updated.Id, "Country", old.Country, updated.Country);
        _auditLog.LogFieldChange("Member", updated.Id, "PostCode", old.PostCode, updated.PostCode);
        _auditLog.LogFieldChange("Member", updated.Id, "City", old.City, updated.City);
        _auditLog.LogFieldChange("Member", updated.Id, "Phone", old.Phone, updated.Phone);
        _auditLog.LogFieldChange("Member", updated.Id, "Email", old.Email, updated.Email);
        _auditLog.LogFieldChange("Member", updated.Id, "DateOfBirth", old.DateOfBirth?.ToString("o"), updated.DateOfBirth?.ToString("o"));
        _auditLog.LogFieldChange("Member", updated.Id, "Citizenship", old.Citizenship, updated.Citizenship);
        _auditLog.LogFieldChange("Member", updated.Id, "MembershipFee", old.MembershipFee, updated.MembershipFee);
        _auditLog.LogFieldChange("Member", updated.Id, "ReducedFee", old.ReducedFee, updated.ReducedFee);
        _auditLog.LogFieldChange("Member", updated.Id, "FirstFee", old.FirstFee, updated.FirstFee);
        _auditLog.LogFieldChange("Member", updated.Id, "OpenFeeTotal", old.OpenFeeTotal, updated.OpenFeeTotal);
        _auditLog.LogFieldChange("Member", updated.Id, "ReducedFeeEnd", old.ReducedFeeEnd?.ToString("o"), updated.ReducedFeeEnd?.ToString("o"));
        _auditLog.LogFieldChange("Member", updated.Id, "EntryDate", old.EntryDate?.ToString("o"), updated.EntryDate?.ToString("o"));
        _auditLog.LogFieldChange("Member", updated.Id, "ExitDate", old.ExitDate?.ToString("o"), updated.ExitDate?.ToString("o"));
        _auditLog.LogFieldChange("Member", updated.Id, "FederalState", old.FederalState, updated.FederalState);
        _auditLog.LogFieldChange("Member", updated.Id, "County", old.County, updated.County);
        _auditLog.LogFieldChange("Member", updated.Id, "Municipality", old.Municipality, updated.Municipality);
        _auditLog.LogFieldChange("Member", updated.Id, "IsPending", old.IsPending.ToString(), updated.IsPending.ToString());
        _auditLog.LogFieldChange("Member", updated.Id, "HasVotingRights", old.HasVotingRights.ToString(), updated.HasVotingRights.ToString());
        _auditLog.LogFieldChange("Member", updated.Id, "ReceivesSurveys", old.ReceivesSurveys.ToString(), updated.ReceivesSurveys.ToString());
        _auditLog.LogFieldChange("Member", updated.Id, "ReceivesActions", old.ReceivesActions.ToString(), updated.ReceivesActions.ToString());
        _auditLog.LogFieldChange("Member", updated.Id, "ReceivesNewsletter", old.ReceivesNewsletter.ToString(), updated.ReceivesNewsletter.ToString());
        _auditLog.LogFieldChange("Member", updated.Id, "PostBounce", old.PostBounce.ToString(), updated.PostBounce.ToString());
        _auditLog.LogFieldChange("Member", updated.Id, "ChapterId", old.ChapterId?.ToString(), updated.ChapterId?.ToString());
        _auditLog.LogFieldChange("Member", updated.Id, "ResidenceAdministrativeDivisionId", old.ResidenceAdministrativeDivisionId?.ToString(), updated.ResidenceAdministrativeDivisionId?.ToString());
        _auditLog.LogFieldChange("Member", updated.Id, "IsImported", old.IsImported.ToString(), updated.IsImported.ToString());
        _auditLog.LogFieldChange("Member", updated.Id, "LastImportedAt", old.LastImportedAt.ToString("o"), updated.LastImportedAt.ToString("o"));
    }

    public void InsertImportLog(MemberImportLog log) => _context.Insert(log);

    public void UpdateImportLog(MemberImportLog log) => _context.Update(log);

    public (List<MemberImportLog> Items, int TotalCount) GetImportHistory(int page, int pageSize) {
        var q = _context.MemberImportLogs.AsQueryable();
        var totalCount = q.Count();
        var items = q.OrderByDescending(l => l.ImportedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        return (items, totalCount);
    }

    /// <summary>Members past their 10-full-year post-exit retention window (year of exit + 11), not yet anonymized.</summary>
    public List<Member> GetEligibleForAnonymization(DateTime now) {
        var thresholdYear = now.Year - 11;
        return _context.Members
            .Where(m => m.AnonymizedAt == null
                && m.ExitDate != null
                && m.ExitDate.Value.Year <= thresholdYear)
            .ToList();
    }

    /// <summary>Nulls PII/financial fields; keeps name + DOB + MemberNumber + ChapterId + entry/exit dates for re-join detection.</summary>
    public void Anonymize(Guid memberId) {
        var now = DateTime.UtcNow;
        _context.Members.Where(m => m.Id == memberId)
            .Set(m => m.AdmissionReference, (string?)null)
            .Set(m => m.Street, (string?)null)
            .Set(m => m.Country, (string?)null)
            .Set(m => m.PostCode, (string?)null)
            .Set(m => m.City, (string?)null)
            .Set(m => m.Phone, (string?)null)
            .Set(m => m.Email, (string?)null)
            .Set(m => m.Citizenship, (string?)null)
            .Set(m => m.FederalState, (string?)null)
            .Set(m => m.County, (string?)null)
            .Set(m => m.Municipality, (string?)null)
            .Set(m => m.ResidenceAdministrativeDivisionId, (Guid?)null)
            .Set(m => m.MembershipFee, 0m)
            .Set(m => m.ReducedFee, 0m)
            .Set(m => m.FirstFee, (decimal?)null)
            .Set(m => m.OpenFeeTotal, (decimal?)null)
            .Set(m => m.ReducedFeeEnd, (DateTime?)null)
            .Set(m => m.AnonymizedAt, (DateTime?)now)
            .Update();
        _auditLog.Log("Member", memberId, "Anonymized");
    }
}
