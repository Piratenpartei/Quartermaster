using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;
using Quartermaster.Api.Forwarding;

namespace Quartermaster.Data.Forwarding;

public class ForwardingSettingsRepository {
    private readonly DbContext _context;

    public ForwardingSettingsRepository(DbContext context) {
        _context = context;
    }

    public ForwardingChapterSetting? GetChapter(Guid chapterId) {
        return _context.ForwardingChapterSettings.FirstOrDefault(s => s.ChapterId == chapterId);
    }

    public List<ForwardingChapterSetting> ListChapters() {
        return _context.ForwardingChapterSettings.ToList();
    }

    /// <summary>Saves the chapter's settings and replaces all of its targets in one transaction.</summary>
    public void SaveChapter(ForwardingChapterSetting setting, IReadOnlyList<ForwardingTarget> targets) {
        using var tx = _context.BeginTransaction();
        _context.ForwardingChapterSettings.Where(s => s.ChapterId == setting.ChapterId).Delete();
        _context.Insert(setting);
        _context.ForwardingTargets.Where(t => t.ChapterId == setting.ChapterId).Delete();
        foreach (var target in targets) {
            target.ChapterId = setting.ChapterId;
            _context.Insert(target);
        }

        tx.Commit();
    }

    public bool DeleteChapter(Guid chapterId) {
        using var tx = _context.BeginTransaction();
        _context.ForwardingTargets.Where(t => t.ChapterId == chapterId).Delete();
        var deleted = _context.ForwardingChapterSettings.Where(s => s.ChapterId == chapterId).Delete() > 0;
        tx.Commit();
        return deleted;
    }

    public ForwardingTarget? GetTarget(Guid chapterId, ForwardingPurpose purpose) {
        return _context.ForwardingTargets.FirstOrDefault(t => t.ChapterId == chapterId && t.Purpose == purpose);
    }

    public List<ForwardingTarget> ListTargets(Guid chapterId) {
        return _context.ForwardingTargets
            .Where(t => t.ChapterId == chapterId)
            .OrderBy(t => t.Purpose)
            .ToList();
    }
}
