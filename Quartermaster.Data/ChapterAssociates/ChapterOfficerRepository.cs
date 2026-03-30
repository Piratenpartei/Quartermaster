using LinqToDB;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Users;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.ChapterAssociates;

public class ChapterOfficerRepository {
    private readonly DbContext _context;

    public ChapterOfficerRepository(DbContext context) {
        _context = context;
    }

    public List<ChapterOfficer> GetForChapter(Guid chapterId)
        => _context.ChapterOfficers.Where(o => o.ChapterId == chapterId).ToList();

    public int CountForChapter(Guid chapterId)
        => _context.ChapterOfficers.Where(o => o.ChapterId == chapterId).Count();

    public void Create(ChapterOfficer officer) => _context.Insert(officer);

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

    private void SeedChapterOfficers(string shortCode, string emailDomain,
        (string FirstName, string LastName, ChapterOfficerType Role)[] officers) {

        var chapter = _context.Chapters
            .Where(c => c.ShortCode == shortCode)
            .FirstOrDefault();
        if (chapter == null)
            return;

        foreach (var (firstName, lastName, role) in officers) {
            var user = new User {
                Id = Guid.NewGuid(),
                FirstName = firstName,
                LastName = lastName,
                EMail = $"{firstName.ToLower().Replace(" ", ".")}.{lastName.ToLower()}@{emailDomain}",
                ChapterId = chapter.Id,
                MemberSince = DateTime.UtcNow
            };
            _context.Insert(user);

            Create(new ChapterOfficer {
                UserId = user.Id,
                ChapterId = chapter.Id,
                AssociateType = role
            });
        }
    }
}
