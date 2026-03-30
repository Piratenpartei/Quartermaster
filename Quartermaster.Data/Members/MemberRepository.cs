using LinqToDB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.Members;

public class MemberRepository {
    private readonly DbContext _context;

    public MemberRepository(DbContext context) {
        _context = context;
    }

    public Member? Get(Guid id)
        => _context.Members.Where(m => m.Id == id).FirstOrDefault();

    public Member? GetByMemberNumber(int memberNumber)
        => _context.Members.Where(m => m.MemberNumber == memberNumber).FirstOrDefault();

    public (List<Member> Items, int TotalCount) Search(
        string? query, Guid? chapterId, int page, int pageSize) {

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

        var totalCount = q.Count();
        var items = q.OrderBy(m => m.LastName).ThenBy(m => m.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (items, totalCount);
    }

    public void Insert(Member member) => _context.Insert(member);

    public void Update(Member member) {
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
