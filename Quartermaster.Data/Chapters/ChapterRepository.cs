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

    public List<Chapter> GetByExternalCode(string externalCode)
        => _context.Chapters.Where(c => c.ExternalCode == externalCode).ToList();

    public Chapter? FindByExternalCodeAndParent(string externalCode, Guid? parentChapterId)
        => _context.Chapters
            .Where(c => c.ExternalCode == externalCode && c.ParentChapterId == parentChapterId)
            .FirstOrDefault();

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

    public List<Guid> GetDescendantIds(Guid chapterId) {
        var result = new List<Guid> { chapterId };
        var queue = new Queue<Guid>();
        queue.Enqueue(chapterId);

        while (queue.Count > 0) {
            var parentId = queue.Dequeue();
            var children = _context.Chapters
                .Where(c => c.ParentChapterId == parentId && c.Id != parentId)
                .Select(c => c.Id)
                .ToList();

            foreach (var childId in children) {
                result.Add(childId);
                queue.Enqueue(childId);
            }
        }

        return result;
    }


    public (List<Chapter> Items, int TotalCount) Search(string? query, int page, int pageSize) {
        var q = _context.Chapters.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query)) {
            q = q.Where(c => c.Name.Contains(query)
                || (c.ShortCode != null && c.ShortCode.Contains(query))
                || (c.ExternalCode != null && c.ExternalCode.Contains(query)));
        }

        var totalCount = q.Count();
        var items = q.OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (items, totalCount);
    }

    public List<Chapter> GetRoots()
        => _context.Chapters.Where(c => c.ParentChapterId == null).OrderBy(c => c.Name).ToList();

    public List<Chapter> GetChildren(Guid parentId)
        => _context.Chapters.Where(c => c.ParentChapterId == parentId && c.Id != parentId).OrderBy(c => c.Name).ToList();

    public List<Chapter> GetAncestorChain(Guid chapterId) {
        var chain = new List<Chapter>();
        var current = Get(chapterId);
        while (current != null) {
            chain.Add(current);
            if (current.ParentChapterId == null || current.ParentChapterId == current.Id)
                break;
            current = Get(current.ParentChapterId.Value);
        }
        return chain;
    }

}
