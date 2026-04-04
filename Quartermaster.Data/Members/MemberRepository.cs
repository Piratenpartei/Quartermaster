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

    public Member? GetByEmail(string email)
        => _context.Members.Where(m => m.EMail == email).FirstOrDefault();

    public Member? GetByUserId(Guid userId)
        => _context.Members.Where(m => m.UserId == userId).FirstOrDefault();

    public void SetUserId(Guid memberId, Guid userId) {
        _context.Members
            .Where(m => m.Id == memberId)
            .Set(m => m.UserId, userId)
            .Update();
    }

    public List<Member> GetByChapterIds(List<Guid> chapterIds) {
        return _context.Members
            .Where(m => m.ChapterId != null && chapterIds.Contains(m.ChapterId.Value))
            .ToList();
    }

    public List<Member> GetByAdministrativeDivisionId(Guid divisionId) {
        return _context.Members
            .Where(m => m.ResidenceAdministrativeDivisionId == divisionId)
            .ToList();
    }

    public List<Member> GetByAdministrativeDivisionIds(List<Guid> divisionIds) {
        return _context.Members
            .Where(m => m.ResidenceAdministrativeDivisionId != null
                && divisionIds.Contains(m.ResidenceAdministrativeDivisionId.Value))
            .ToList();
    }

    public (List<Member> Items, int TotalCount) Search(
        string? query, Guid? chapterId, int page, int pageSize,
        List<Guid>? allowedChapterIds = null, bool orphanedOnly = false) {

        var q = _context.Members.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query)) {
            if (int.TryParse(query, out var memberNum)) {
                q = q.Where(m => m.MemberNumber == memberNum);
            } else {
                q = q.Where(m => m.FirstName.Contains(query)
                    || m.LastName.Contains(query)
                    || (m.EMail != null && m.EMail.Contains(query)));
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

    public void Update(Member member) {
        var existing = _context.Members.Where(m => m.Id == member.Id).FirstOrDefault();

        _context.Members
            .Where(m => m.Id == member.Id)
            .Set(m => m.AdmissionReference, member.AdmissionReference)
            .Set(m => m.FirstName, member.FirstName)
            .Set(m => m.LastName, member.LastName)
            .Set(m => m.Street, member.Street)
            .Set(m => m.Country, member.Country)
            .Set(m => m.PostCode, member.PostCode)
            .Set(m => m.City, member.City)
            .Set(m => m.Phone, member.Phone)
            .Set(m => m.EMail, member.EMail)
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
            .Set(m => m.LastImportedAt, member.LastImportedAt)
            .Update();

        if (existing != null)
            LogMemberFieldChanges(existing, member);
    }

    private void LogMemberFieldChanges(Member old, Member updated) {
        _auditLog.LogFieldChange("Member", updated.Id, "AdmissionReference", old.AdmissionReference, updated.AdmissionReference);
        _auditLog.LogFieldChange("Member", updated.Id, "FirstName", old.FirstName, updated.FirstName);
        _auditLog.LogFieldChange("Member", updated.Id, "LastName", old.LastName, updated.LastName);
        _auditLog.LogFieldChange("Member", updated.Id, "Street", old.Street, updated.Street);
        _auditLog.LogFieldChange("Member", updated.Id, "Country", old.Country, updated.Country);
        _auditLog.LogFieldChange("Member", updated.Id, "PostCode", old.PostCode, updated.PostCode);
        _auditLog.LogFieldChange("Member", updated.Id, "City", old.City, updated.City);
        _auditLog.LogFieldChange("Member", updated.Id, "Phone", old.Phone, updated.Phone);
        _auditLog.LogFieldChange("Member", updated.Id, "EMail", old.EMail, updated.EMail);
        _auditLog.LogFieldChange("Member", updated.Id, "DateOfBirth", old.DateOfBirth?.ToString("o"), updated.DateOfBirth?.ToString("o"));
        _auditLog.LogFieldChange("Member", updated.Id, "Citizenship", old.Citizenship, updated.Citizenship);
        _auditLog.LogFieldChange("Member", updated.Id, "MembershipFee", old.MembershipFee.ToString(), updated.MembershipFee.ToString());
        _auditLog.LogFieldChange("Member", updated.Id, "ReducedFee", old.ReducedFee.ToString(), updated.ReducedFee.ToString());
        _auditLog.LogFieldChange("Member", updated.Id, "FirstFee", old.FirstFee?.ToString(), updated.FirstFee?.ToString());
        _auditLog.LogFieldChange("Member", updated.Id, "OpenFeeTotal", old.OpenFeeTotal?.ToString(), updated.OpenFeeTotal?.ToString());
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
        _auditLog.LogFieldChange("Member", updated.Id, "LastImportedAt", old.LastImportedAt.ToString("o"), updated.LastImportedAt.ToString("o"));
    }

    public void InsertImportLog(MemberImportLog log) => _context.Insert(log);

    public (List<MemberImportLog> Items, int TotalCount) GetImportHistory(int page, int pageSize) {
        var q = _context.MemberImportLogs.AsQueryable();
        var totalCount = q.Count();
        var items = q.OrderByDescending(l => l.ImportedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        return (items, totalCount);
    }
}
