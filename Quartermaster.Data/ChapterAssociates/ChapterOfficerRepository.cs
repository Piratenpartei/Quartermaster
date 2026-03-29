using LinqToDB;
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
}
