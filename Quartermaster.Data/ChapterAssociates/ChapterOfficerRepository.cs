using LinqToDB;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Members;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.ChapterAssociates;

public class ChapterOfficerRepository {
    private readonly DbContext _context;
    private readonly AuditLogRepository _auditLog;

    public ChapterOfficerRepository(DbContext context, AuditLogRepository auditLog) {
        _context = context;
        _auditLog = auditLog;
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

    public void Delete(Guid memberId, Guid chapterId) {
        _context.ChapterOfficers
            .Where(o => o.MemberId == memberId && o.ChapterId == chapterId)
            .Delete();
        _auditLog.LogDeleted("ChapterOfficer", memberId);
        _auditLog.Log("ChapterOfficer", memberId, "Deleted", "ChapterId", chapterId.ToString(), null);
    }

    public void SupplementDefaults(ChapterRepository chapterRepo) {
        if (_context.ChapterOfficers.Any())
            return;

        SeedChapterOfficers("de", "piratenpartei.de", new[] {
            ("Lilia Kayra", "Kuyumcu", ChapterOfficerType.Captain),
            ("Dennis", "Klüver", ChapterOfficerType.FirstOfficer),
            ("Jutta", "Dietrich", ChapterOfficerType.Treasurer),
            ("Babak", "Tubis", ChapterOfficerType.Member),
            ("Karsten", "Wehner", ChapterOfficerType.Member),
            ("Nick", "Neumann", ChapterOfficerType.Member),
            ("Wolf Vincent", "Lübcke", ChapterOfficerType.Member),
        });

        SeedChapterOfficers("nds", "piratenpartei-nds.de", new[] {
            ("Thomas", "Ganskow", ChapterOfficerType.Captain),
            ("Joscha", "Germerott", ChapterOfficerType.FirstOfficer),
            ("Uwe", "Kopec", ChapterOfficerType.Treasurer),
            ("Danny", "Hartmann", ChapterOfficerType.ViceTreasurer),
            ("Olaf", "Engel", ChapterOfficerType.Member),
            ("Richard", "Klaus", ChapterOfficerType.Member),
            ("Darius Nikolaus", "Krupinski", ChapterOfficerType.Member),
        });
    }

    private static int _seedMemberCounter = -1;

    private void SeedChapterOfficers(string shortCode, string emailDomain,
        (string FirstName, string LastName, ChapterOfficerType Role)[] officers) {

        var chapter = _context.Chapters
            .Where(c => c.ShortCode == shortCode)
            .FirstOrDefault();
        if (chapter == null)
            return;

        foreach (var (firstName, lastName, role) in officers) {
            // Find existing member by name, or create one
            var member = _context.Members
                .Where(m => m.FirstName == firstName && m.LastName == lastName)
                .FirstOrDefault();

            if (member == null) {
                member = new Member {
                    Id = Guid.NewGuid(),
                    MemberNumber = _seedMemberCounter--,
                    FirstName = firstName,
                    LastName = lastName,
                    EMail = $"{firstName.ToLower().Replace(" ", ".")}.{lastName.ToLower()}@{emailDomain}",
                    LastImportedAt = DateTime.UtcNow
                };
                _context.Insert(member);
            }

            Create(new ChapterOfficer {
                MemberId = member.Id,
                ChapterId = chapter.Id,
                AssociateType = role
            });
        }
    }
}
