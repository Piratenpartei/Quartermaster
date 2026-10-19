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

    public void Update(Chapter chapter) {
        _context.Chapters
            .Where(c => c.Id == chapter.Id)
            .Set(c => c.Name, chapter.Name)
            .Set(c => c.ShortCode, chapter.ShortCode)
            .Set(c => c.KeycloakDepartment, chapter.KeycloakDepartment)
            .Set(c => c.ParentChapterId, chapter.ParentChapterId)
            .Update();
    }

    /// <summary>The chapter whose Keycloak department equals <paramref name="department"/>, ignoring case and surrounding spaces.</summary>
    public Chapter? GetByKeycloakDepartment(string department, Guid? excludeChapterId = null) {
        var value = department.Trim().ToLowerInvariant();
        return _context.Chapters
            .Where(c => c.DeletedAt == null && c.KeycloakDepartment != null && c.KeycloakDepartment.ToLower() == value)
            .Where(c => excludeChapterId == null || c.Id != excludeChapterId)
            .FirstOrDefault();
    }

    /// <summary>Replaces the chapter's external-code set with the supplied list (deduped); caller owns trim/empty-drop.</summary>
    public void ReplaceExternalCodes(Guid chapterId, IEnumerable<string> codes) {
        _context.ChapterExternalCodes.Where(c => c.ChapterId == chapterId).Delete();
        foreach (var code in codes.Distinct()) {
            _context.Insert(new ChapterExternalCode { ChapterId = chapterId, Code = code });
        }
    }

    public void ReplaceAdministrativeDivisions(Guid chapterId, IEnumerable<Guid> divisionIds) {
        _context.ChapterAdministrativeDivisions.Where(c => c.ChapterId == chapterId).Delete();
        foreach (var divisionId in divisionIds.Distinct()) {
            _context.Insert(new ChapterAdministrativeDivision {
                ChapterId = chapterId,
                AdministrativeDivisionId = divisionId
            });
        }
    }

    public List<string> GetExternalCodes(Guid chapterId)
        => _context.ChapterExternalCodes
            .Where(c => c.ChapterId == chapterId)
            .OrderBy(c => c.Code)
            .Select(c => c.Code)
            .ToList();

    public List<Guid> GetAdministrativeDivisions(Guid chapterId)
        => _context.ChapterAdministrativeDivisions
            .Where(c => c.ChapterId == chapterId)
            .Select(c => c.AdministrativeDivisionId)
            .ToList();

    /// <summary>Batch-loads the external-code list for the given chapter ids, keyed by chapter id. Missing keys ⇒ no codes.</summary>
    public Dictionary<Guid, List<string>> LoadExternalCodesFor(IReadOnlyCollection<Guid> chapterIds) {
        if (chapterIds.Count == 0)
            return [];
        return _context.ChapterExternalCodes
            .Where(c => chapterIds.Contains(c.ChapterId))
            .ToList()
            .GroupBy(c => c.ChapterId)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Code).OrderBy(s => s).ToList());
    }

    /// <summary>Builds DTOs with codes + divisions batch-loaded — two extra queries total regardless of list size.</summary>
    public List<Api.Chapters.ChapterDTO> ToDtos(IReadOnlyList<Chapter> chapters) {
        if (chapters.Count == 0)
            return [];
        var ids = chapters.Select(c => c.Id).ToList();
        var codesByChapter = LoadExternalCodesFor(ids);
        var divisionsByChapter = LoadAdministrativeDivisionsFor(ids);
        return chapters.Select(c => c.ToDto(
            codesByChapter.GetValueOrDefault(c.Id, []),
            divisionsByChapter.GetValueOrDefault(c.Id, []))).ToList();
    }

    public Dictionary<Guid, List<Guid>> LoadAdministrativeDivisionsFor(IReadOnlyCollection<Guid> chapterIds) {
        if (chapterIds.Count == 0)
            return [];
        return _context.ChapterAdministrativeDivisions
            .Where(c => chapterIds.Contains(c.ChapterId))
            .ToList()
            .GroupBy(c => c.ChapterId)
            .ToDictionary(g => g.Key, g => g.Select(c => c.AdministrativeDivisionId).ToList());
    }

    public bool HasNonDeletedChildren(Guid chapterId)
        => _context.Chapters.Any(c => c.ParentChapterId == chapterId && c.DeletedAt == null);

    public void SoftDelete(Guid id)
        => _context.Chapters.Where(c => c.Id == id).Set(c => c.DeletedAt, DateTime.UtcNow).Update();

    /// <summary>Archive ≠ delete: archived chapters stay visible so member-import CSVs can still resolve historical
    /// assignments, but new motions / events / meetings are blocked and membership-application auto-assignment
    /// skips them. Pass <paramref name="archived"/>=false to restore.</summary>
    public void SetArchived(Guid id, bool archived) {
        var now = archived ? DateTime.UtcNow : (DateTime?)null;
        _context.Chapters.Where(c => c.Id == id).Set(c => c.ArchivedAt, now).Update();
        _auditLog.LogFieldChange("Chapter", id, "ArchivedAt",
            archived ? null : "set", archived ? "set" : null);
    }

    public List<Chapter> GetByExternalCode(string externalCode) {
        var matchingIds = _context.ChapterExternalCodes
            .Where(c => c.Code == externalCode)
            .Select(c => c.ChapterId)
            .ToList();
        if (matchingIds.Count == 0)
            return [];
        return _context.Chapters
            .Where(c => matchingIds.Contains(c.Id) && c.DeletedAt == null)
            .ToList();
    }

    /// <summary>Returns any chapter under the given parent whose code set overlaps any of <paramref name="codes"/>.
    /// Excludes <paramref name="excludeChapterId"/> from the check so a chapter editing its own codes doesn't conflict with itself.</summary>
    public Chapter? FindCodeConflictUnderParent(IEnumerable<string> codes, Guid? parentChapterId, Guid? excludeChapterId = null) {
        var codeList = codes.ToList();
        if (codeList.Count == 0)
            return null;

        var conflictChapterIds = _context.ChapterExternalCodes
            .Where(c => codeList.Contains(c.Code))
            .Select(c => c.ChapterId)
            .Distinct()
            .ToList();
        if (conflictChapterIds.Count == 0)
            return null;

        return _context.Chapters
            .Where(c => conflictChapterIds.Contains(c.Id)
                && c.ParentChapterId == parentChapterId
                && c.DeletedAt == null
                && (excludeChapterId == null || c.Id != excludeChapterId.Value))
            .FirstOrDefault();
    }

    public Chapter? FindForDivision(Guid divisionId, AdministrativeDivisions.AdministrativeDivisionRepository adminDivRepo) {
        var ancestorIds = adminDivRepo.GetAncestorIds(divisionId);
        if (ancestorIds.Count == 0)
            return GetRootChapter();

        var ancestorChapterIds = _context.ChapterAdministrativeDivisions
            .Where(c => ancestorIds.Contains(c.AdministrativeDivisionId))
            .ToList();

        // Most specific match wins: the chapter whose division appears earliest in the ancestor chain.
        // Archived chapters skipped — historical reference only; new applications route to a live chapter.
        foreach (var ancestorId in ancestorIds) {
            var matchId = ancestorChapterIds
                .FirstOrDefault(c => c.AdministrativeDivisionId == ancestorId)?.ChapterId;
            if (matchId == null)
                continue;
            var chapter = _context.Chapters
                .Where(c => c.Id == matchId.Value && c.DeletedAt == null && c.ArchivedAt == null)
                .FirstOrDefault();
            if (chapter != null)
                return chapter;
        }

        // No region-specific chapter covers this division — fall back to the top-level
        // (national) chapter so an application still routes to a reviewable body.
        return GetRootChapter();
    }

    public Chapter? GetRootChapter() {
        return _context.Chapters
            .Where(c => c.ParentChapterId == null && c.DeletedAt == null)
            .OrderBy(c => c.Name)
            .FirstOrDefault();
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
            var codeMatchedIds = _context.ChapterExternalCodes
                .Where(c => c.Code.Contains(query))
                .Select(c => c.ChapterId)
                .Distinct()
                .ToList();
            q = q.Where(c => c.Name.Contains(query)
                || (c.ShortCode != null && c.ShortCode.Contains(query))
                || codeMatchedIds.Contains(c.Id));
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
