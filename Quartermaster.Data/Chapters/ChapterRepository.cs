using LinqToDB;
using Quartermaster.Data.AuditLog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.Chapters;

public class ChapterRepository {
    private readonly DbContext _context;
    private readonly AuditLogRepository _auditLog;

    public ChapterRepository(DbContext context, AuditLogRepository auditLog) {
        _context = context;
        _auditLog = auditLog;
    }

    public Chapter? Get(Guid id)
        => _context.Chapters.Where(c => c.Id == id && c.DeletedAt == null).FirstOrDefault();

    public List<Chapter> GetAll()
        => _context.Chapters.Where(c => c.DeletedAt == null).OrderBy(c => c.Name).ToList();

    public void Create(Chapter chapter) => _context.Insert(chapter);

    public List<Chapter> GetByExternalCode(string externalCode)
        => _context.Chapters.Where(c => c.ExternalCode == externalCode && c.DeletedAt == null).ToList();

    public Chapter? GetByExternalCodeAndParent(string externalCode, Guid? parentChapterId)
        => _context.Chapters
            .Where(c => c.ExternalCode == externalCode && c.ParentChapterId == parentChapterId && c.DeletedAt == null)
            .FirstOrDefault();

    public Chapter? FindForDivision(Guid divisionId, AdministrativeDivisions.AdministrativeDivisionRepository adminDivRepo) {
        var ancestorIds = adminDivRepo.GetAncestorIds(divisionId);
        if (ancestorIds.Count == 0)
            return null;

        var chapters = _context.Chapters
            .Where(c => c.AdministrativeDivisionId != null
                && ancestorIds.Contains(c.AdministrativeDivisionId.Value)
                && c.DeletedAt == null)
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

    private class ChapterTreeRow {
        public Guid Id { get; set; }
        public Guid? ParentChapterId { get; set; }
        public int Depth { get; set; }
    }

    public List<Guid> GetDescendantIds(Guid chapterId) {
        var cte = _context.GetCte<ChapterTreeRow>(self =>
            _context.Chapters
                .Where(c => c.Id == chapterId && c.DeletedAt == null)
                .Select(c => new ChapterTreeRow { Id = c.Id, ParentChapterId = c.ParentChapterId, Depth = 0 })
                .Concat(self.SelectMany(prev => _context.Chapters
                    .InnerJoin(c => c.ParentChapterId == prev.Id && c.Id != prev.Id && c.DeletedAt == null)
                    .Select(c => new ChapterTreeRow { Id = c.Id, ParentChapterId = c.ParentChapterId, Depth = prev.Depth + 1 }))));

        return cte.OrderBy(r => r.Depth).Select(r => r.Id).ToList();
    }


    public (List<Chapter> Items, int TotalCount) Search(string? query, int page, int pageSize) {
        var q = _context.Chapters.Where(c => c.DeletedAt == null).AsQueryable();

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
        => _context.Chapters.Where(c => c.ParentChapterId == null && c.DeletedAt == null).OrderBy(c => c.Name).ToList();

    public List<Chapter> GetChildren(Guid parentId)
        => _context.Chapters
            .Where(c => c.ParentChapterId == parentId && c.Id != parentId && c.DeletedAt == null)
            .OrderBy(c => c.Name).ToList();

    public List<Guid> GetAncestorChainIds(Guid chapterId) {
        var cte = _context.GetCte<ChapterTreeRow>(self =>
            _context.Chapters
                .Where(c => c.Id == chapterId && c.DeletedAt == null)
                .Select(c => new ChapterTreeRow { Id = c.Id, ParentChapterId = c.ParentChapterId, Depth = 0 })
                .Concat(self.SelectMany(prev => _context.Chapters
                    .InnerJoin(c => c.Id == prev.ParentChapterId && c.Id != prev.Id && c.DeletedAt == null)
                    .Select(c => new ChapterTreeRow { Id = c.Id, ParentChapterId = c.ParentChapterId, Depth = prev.Depth + 1 }))));

        return cte.OrderBy(r => r.Depth).Select(r => r.Id).ToList();
    }

    public List<Chapter> GetAncestorChain(Guid chapterId) {
        var orderedIds = GetAncestorChainIds(chapterId);
        if (orderedIds.Count == 0)
            return [];

        var chapters = _context.Chapters.Where(c => orderedIds.Contains(c.Id)).ToList()
            .ToDictionary(c => c.Id);
        return orderedIds.Where(chapters.ContainsKey).Select(id => chapters[id]).ToList();
    }

    public enum ChapterDeleteResult {
        Success,
        NotFound,
        IsRoot
    }

    /// <summary>Root chapters are refused. Members reassign to parent; per-chapter junctions hard-delete; everything else soft-deletes.</summary>
    public ChapterDeleteResult SoftDeleteWithCascade(Guid chapterId) {
        var chapter = Get(chapterId);
        if (chapter == null)
            return ChapterDeleteResult.NotFound;
        if (chapter.ParentChapterId == null)
            return ChapterDeleteResult.IsRoot;
        var parentId = chapter.ParentChapterId.Value;
        var now = DateTime.UtcNow;

        using var tx = _context.BeginTransaction();

        _context.Events.Where(e => e.ChapterId == chapterId && e.DeletedAt == null)
            .Set(e => e.DeletedAt, (DateTime?)now).Update();
        _context.Meetings.Where(m => m.ChapterId == chapterId && m.DeletedAt == null)
            .Set(m => m.DeletedAt, (DateTime?)now).Update();
        _context.Motions.Where(m => m.ChapterId == chapterId && m.DeletedAt == null)
            .Set(m => m.DeletedAt, (DateTime?)now).Update();

        var dueSelectionIds = _context.MembershipApplications
            .Where(a => a.ChapterId == chapterId && a.DueSelectionId != null)
            .Select(a => a.DueSelectionId!.Value)
            .ToList();
        if (dueSelectionIds.Count > 0) {
            _context.DueSelections.Where(d => dueSelectionIds.Contains(d.Id) && d.DeletedAt == null)
                .Set(d => d.DeletedAt, (DateTime?)now).Update();
        }
        _context.MembershipApplications.Where(a => a.ChapterId == chapterId && a.DeletedAt == null)
            .Set(a => a.DeletedAt, (DateTime?)now).Update();

        _context.Members.Where(m => m.ChapterId == chapterId)
            .Set(m => m.ChapterId, (Guid?)parentId).Update();

        _context.ChapterOfficers.Where(o => o.ChapterId == chapterId).Delete();
        _context.UserChapterPermissions.Where(p => p.ChapterId == chapterId).Delete();
        _context.UserRoleAssignments.Where(a => a.ChapterId == chapterId).Delete();

        _context.Chapters.Where(c => c.Id == chapterId)
            .Set(c => c.DeletedAt, (DateTime?)now).Update();
        _auditLog.LogSoftDeleted("Chapter", chapterId);

        tx.Commit();
        return ChapterDeleteResult.Success;
    }
}
