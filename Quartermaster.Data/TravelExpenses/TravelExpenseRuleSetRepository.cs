using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;
using Quartermaster.Api.TravelExpenses;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Chapters;

namespace Quartermaster.Data.TravelExpenses;

public class TravelExpenseRuleSetRepository {
    private const string AuditEntity = "TravelExpenseRuleSet";

    private readonly DbContext _context;
    private readonly AuditLogRepository _auditLog;

    public TravelExpenseRuleSetRepository(DbContext context, AuditLogRepository auditLog) {
        _context = context;
        _auditLog = auditLog;
    }

    public TravelExpenseRuleSet? Get(Guid id) {
        return _context.TravelExpenseRuleSets.FirstOrDefault(r => r.Id == id);
    }

    /// <summary>The versions of one chapter, or of the global default for null, newest first.</summary>
    public List<TravelExpenseRuleSet> ListFor(Guid? chapterId) {
        return _context.TravelExpenseRuleSets
            .Where(r => r.ChapterId == chapterId)
            .OrderByDescending(r => r.ValidFrom)
            .ToList();
    }

    /// <summary>Two versions of one chapter valid from the same day would make the lookup ambiguous.</summary>
    public bool HasVersionFrom(Guid? chapterId, DateOnly validFrom, Guid? exceptId) {
        var day = validFrom.ToDateTime(TimeOnly.MinValue);
        return _context.TravelExpenseRuleSets.Any(r => r.ChapterId == chapterId && r.ValidFrom == day && r.Id != exceptId);
    }

    public void Create(TravelExpenseRuleSet ruleSet) {
        _context.Insert(ruleSet);
        _auditLog.LogCreated(AuditEntity, ruleSet.Id);
    }

    public void Update(TravelExpenseRuleSet ruleSet) {
        _context.Update(ruleSet);
        _auditLog.Log(AuditEntity, ruleSet.Id, "Updated");
    }

    public void Delete(Guid id) {
        _context.TravelExpenseRuleSets
            .Where(r => r.Id == id)
            .Delete();
        _auditLog.LogDeleted(AuditEntity, id);
    }

    /// <summary>
    /// The rules for a trip of the chapter starting on the date: the newest version valid then, from the chapter itself
    /// up to the root, then the global default. A trip before every global version gets the oldest one.
    /// </summary>
    public TravelExpenseRuleSet? Resolve(Guid? chapterId, DateOnly date, ChapterRepository chapters) {
        var day = date.ToDateTime(TimeOnly.MinValue);
        var chain = chapterId == null ? new List<Guid>() : chapters.GetAncestorChainIds(chapterId.Value);
        var candidates = _context.TravelExpenseRuleSets
            .Where(r => r.ChapterId == null || chain.Contains(r.ChapterId.Value))
            .ToList();

        foreach (var id in chain) {
            var own = Newest(candidates.Where(r => r.ChapterId == id), day);
            if (own != null)
                return own;
        }

        var globals = candidates
            .Where(r => r.ChapterId == null)
            .ToList();
        return Newest(globals, day) ?? globals.OrderBy(r => r.ValidFrom).FirstOrDefault();
    }

    private static TravelExpenseRuleSet? Newest(IEnumerable<TravelExpenseRuleSet> ruleSets, DateTime day) {
        return ruleSets
            .Where(r => r.ValidFrom <= day)
            .OrderByDescending(r => r.ValidFrom)
            .FirstOrDefault();
    }

    /// <summary>Seeds the federal rules as the global default when there is none.</summary>
    public void SupplementDefaults() {
        if (_context.TravelExpenseRuleSets.Any(r => r.ChapterId == null))
            return;

        var ruleSet = new TravelExpenseRuleSet { UpdatedAt = DateTime.UtcNow };
        ruleSet.Apply(TravelExpenseRulePresets.Federal());
        _context.Insert(ruleSet);
    }
}
