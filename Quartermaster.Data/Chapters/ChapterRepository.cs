using LinqToDB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.Chapters;

public class ChapterRepository {
    private readonly DbContext _context;

    public ChapterRepository(DbContext context) {
        _context = context;
    }

    public Chapter? Get(Guid id)
        => _context.Chapters.Where(c => c.Id == id).FirstOrDefault();

    public List<Chapter> GetAll()
        => _context.Chapters.OrderBy(c => c.Name).ToList();

    public void Create(Chapter chapter) => _context.Insert(chapter);

    public Chapter? FindForDivision(Guid divisionId, AdministrativeDivisions.AdministrativeDivisionRepository adminDivRepo) {
        var ancestorIds = adminDivRepo.GetAncestorIds(divisionId);
        if (ancestorIds.Count == 0)
            return null;

        var chapters = _context.Chapters
            .Where(c => c.AdministrativeDivisionId != null && ancestorIds.Contains(c.AdministrativeDivisionId.Value))
            .ToList();

        if (chapters.Count == 0)
            return null;

        // Return the chapter whose division appears earliest in ancestor list (most specific)
        foreach (var ancestorId in ancestorIds) {
            var match = chapters.FirstOrDefault(c => c.AdministrativeDivisionId == ancestorId);
            if (match != null)
                return match;
        }

        return chapters[0];
    }

    public void SupplementDefaults(AdministrativeDivisions.AdministrativeDivisionRepository adminDivRepo) {
        if (_context.Chapters.Any())
            return;

        var deDivision = _context.GetTable<AdministrativeDivisions.AdministrativeDivision>()
            .Where(ad => ad.AdminCode == "DE" && ad.Depth == 3)
            .FirstOrDefault();
        if (deDivision == null)
            return;

        var bundesverband = new Chapter {
            Id = Guid.NewGuid(),
            Name = "Piratenpartei Deutschland",
            AdministrativeDivisionId = deDivision.Id,
            ParentChapterId = null
        };
        Create(bundesverband);

        var states = adminDivRepo.GetChildren(deDivision.Id);
        foreach (var state in states) {
            Create(new Chapter {
                Id = Guid.NewGuid(),
                Name = $"Piratenpartei {state.Name}",
                AdministrativeDivisionId = state.Id,
                ParentChapterId = bundesverband.Id
            });
        }
    }
}
