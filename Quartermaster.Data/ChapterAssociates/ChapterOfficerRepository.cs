using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Members;
using Quartermaster.Data.Roles;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.ChapterAssociates;

public class ChapterOfficerRepository {
    private readonly DbContext _context;
    private readonly AuditLogRepository _auditLog;
    private readonly RoleRepository _roleRepo;

    public ChapterOfficerRepository(DbContext context, AuditLogRepository auditLog, RoleRepository roleRepo) {
        _context = context;
        _auditLog = auditLog;
        _roleRepo = roleRepo;
    }

    public List<ChapterOfficer> GetForChapter(Guid chapterId)
        => _context.ChapterOfficers.Where(o => o.ChapterId == chapterId).ToList();

    public int CountForChapter(Guid chapterId)
        => _context.ChapterOfficers.Where(o => o.ChapterId == chapterId).Count();

    public void Create(ChapterOfficer officer) {
        _context.Insert(officer);
        _auditLog.LogCreated("ChapterOfficer", officer.MemberId);
        _auditLog.Log("ChapterOfficer", officer.MemberId, "Created", "ChapterId", null, officer.ChapterId.ToString());
    }

    public (List<(ChapterOfficer Officer, Member Member, Chapter Chapter)> Items, int TotalCount) SearchAll(
        string? query, Guid? chapterId, int page, int pageSize) {

        var q = from o in _context.ChapterOfficers
                join m in _context.Members on o.MemberId equals m.Id
                join c in _context.Chapters on o.ChapterId equals c.Id
                select new { Officer = o, Member = m, Chapter = c };

        if (chapterId.HasValue)
            q = q.Where(x => x.Officer.ChapterId == chapterId.Value);

        if (!string.IsNullOrWhiteSpace(query)) {
            if (int.TryParse(query, out var memberNum)) {
                q = q.Where(x => x.Member.MemberNumber == memberNum);
            } else {
                q = q.Where(x => x.Member.FirstName.Contains(query) || x.Member.LastName.Contains(query));
            }
        }

        var totalCount = q.Count();
        var items = q.OrderBy(x => x.Chapter.Name).ThenBy(x => x.Member.LastName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList()
            .Select(x => (x.Officer, x.Member, x.Chapter))
            .ToList();

        return (items, totalCount);
    }

    public bool IsOfficerByUserId(Guid userId, Guid chapterId) {
        return _context.Members
            .Where(m => m.UserId == userId)
            .InnerJoin(_context.ChapterOfficers,
                (m, o) => o.MemberId == m.Id && o.ChapterId == chapterId,
                (m, o) => o)
            .Any();
    }

    public bool IsOfficerByUserIdForAnyChapter(Guid userId, List<Guid> chapterIds) {
        if (chapterIds.Count == 0)
            return false;

        return _context.Members
            .Where(m => m.UserId == userId)
            .InnerJoin(_context.ChapterOfficers,
                (m, o) => o.MemberId == m.Id && chapterIds.Contains(o.ChapterId),
                (m, o) => o)
            .Any();
    }

    public void GrantDefaultPermissions(Guid userId, Guid chapterId) {
        var officerRole = _roleRepo.GetByIdentifier(PermissionIdentifier.SystemRole.ChapterOfficer);
        if (officerRole == null)
            return;
        _roleRepo.Assign(userId, officerRole.Id, chapterId);
    }

    public void RevokeDefaultPermissions(Guid userId, Guid chapterId) {
        var officerRole = _roleRepo.GetByIdentifier(PermissionIdentifier.SystemRole.ChapterOfficer);
        if (officerRole == null)
            return;
        _roleRepo.Revoke(userId, officerRole.Id, chapterId);
    }

    public void GrantDefaultPermissionsForAllChapters(Guid memberId, Guid userId) {
        var officerEntries = _context.ChapterOfficers
            .Where(o => o.MemberId == memberId)
            .ToList();

        foreach (var entry in officerEntries)
            GrantDefaultPermissions(userId, entry.ChapterId);
    }

    public void Delete(Guid memberId, Guid chapterId) {
        _context.ChapterOfficers
            .Where(o => o.MemberId == memberId && o.ChapterId == chapterId)
            .Delete();
        _auditLog.LogDeleted("ChapterOfficer", memberId);
        _auditLog.Log("ChapterOfficer", memberId, "Deleted", "ChapterId", chapterId.ToString(), null);
    }
}
