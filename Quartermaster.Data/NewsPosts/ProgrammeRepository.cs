using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;

namespace Quartermaster.Data.NewsPosts;

public class ProgrammeRepository {
    private readonly DbContext _context;

    public ProgrammeRepository(DbContext context) {
        _context = context;
    }

    public List<ProgrammeSource> GetSources()
        => _context.ProgrammeSources.OrderBy(s => s.WikiPageTitle).ToList();

    public ProgrammeSource? GetSource(Guid id)
        => _context.ProgrammeSources.Where(s => s.Id == id).FirstOrDefault();

    public bool SourceExists(string wikiPageTitle)
        => _context.ProgrammeSources.Any(s => s.ChapterId == null && s.WikiPageTitle == wikiPageTitle);

    public void CreateSource(ProgrammeSource source)
        => _context.Insert(source);

    public void DeleteSource(Guid id) {
        _context.ProgrammeSections.Where(s => s.SourceId == id).Delete();
        _context.ProgrammeSources.Where(s => s.Id == id).Delete();
    }

    public void MarkSourceSynced(Guid id, long? revisionId, DateTime now) {
        _context.ProgrammeSources
            .Where(s => s.Id == id)
            .Set(s => s.LastRevisionId, revisionId)
            .Set(s => s.LastSyncedAt, now)
            .Set(s => s.LastError, (string?)null)
            .Update();
    }

    public void MarkSourceFailed(Guid id, string error) {
        _context.ProgrammeSources
            .Where(s => s.Id == id)
            .Set(s => s.LastError, error)
            .Update();
    }

    public List<ProgrammeSection> GetSections(Guid sourceId, string pageTitle)
        => _context.ProgrammeSections.Where(s => s.SourceId == sourceId && s.PageTitle == pageTitle).ToList();

    public List<ProgrammeSection> GetAllSections()
        => _context.ProgrammeSections.ToList();

    public List<ProgrammeSection> GetSectionsByIds(IReadOnlyCollection<Guid> ids)
        => _context.ProgrammeSections.Where(s => ids.Contains(s.Id)).ToList();

    public int CountSections(Guid sourceId)
        => _context.ProgrammeSections.Count(s => s.SourceId == sourceId);

    public List<string> GetPageTitles(Guid sourceId)
        => _context.ProgrammeSections
            .Where(s => s.SourceId == sourceId)
            .Select(s => s.PageTitle)
            .Distinct()
            .ToList();

    /// <summary>Replaces the stored sections of one page: inserts <paramref name="added"/>, updates <paramref name="changed"/>, deletes <paramref name="removedIds"/>. Runs in one transaction so a failure keeps the previous state.</summary>
    public void ApplyPageDiff(List<ProgrammeSection> added, List<ProgrammeSection> changed, List<Guid> removedIds) {
        using var tx = _context.BeginTransaction();
        foreach (var section in added)
            _context.Insert(section);
        foreach (var section in changed)
            _context.Update(section);
        if (removedIds.Count > 0)
            _context.ProgrammeSections.Where(s => removedIds.Contains(s.Id)).Delete();
        tx.Commit();
    }

    public void DeletePage(Guid sourceId, string pageTitle) {
        _context.ProgrammeSections
            .Where(s => s.SourceId == sourceId && s.PageTitle == pageTitle)
            .Delete();
    }
}
