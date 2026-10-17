using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.PublicForms;

/// <summary>Reads the switches for the public membership application, due selection and motion forms.</summary>
public class PublicFormSettings {
    private readonly OptionRepository _options;
    private readonly ChapterRepository _chapters;

    public PublicFormSettings(OptionRepository options, ChapterRepository chapters) {
        _options = options;
        _chapters = chapters;
    }

    public bool IsMembershipApplicationEnabled() {
        return IsTrue(_options.GetGlobalValue(PublicFormOptions.MembershipApplication)?.Value);
    }

    public bool IsDueSelectionEnabled() {
        return IsTrue(_options.GetGlobalValue(PublicFormOptions.DueSelection)?.Value);
    }

    public bool IsMotionFormEnabled(Guid chapterId) {
        return IsTrue(_options.ResolveValue(PublicFormOptions.Motion, chapterId, _chapters));
    }

    /// <summary>All non-archived chapters that take motions through the public form, resolved in memory in one pass.</summary>
    public List<Guid> MotionFormChapterIds() {
        var values = _options.GetValues(PublicFormOptions.Motion);
        var global = IsTrue(values.FirstOrDefault(v => v.ChapterId == null)?.Value);
        var overrides = values
            .Where(v => v.ChapterId != null)
            .ToDictionary(v => v.ChapterId!.Value, v => IsTrue(v.Value));
        var chapters = _chapters.GetAll();
        var parents = chapters.ToDictionary(c => c.Id, c => c.ParentChapterId);

        return chapters
            .Where(c => c.ArchivedAt == null && Resolve(c.Id, parents, overrides, global))
            .Select(c => c.Id)
            .ToList();
    }

    // The hop limit guards against a corrupt parent cycle.
    private static bool Resolve(Guid chapterId, Dictionary<Guid, Guid?> parents, Dictionary<Guid, bool> overrides, bool global) {
        Guid? current = chapterId;
        for (var hops = 0; current != null && hops <= parents.Count; hops++) {
            if (overrides.TryGetValue(current.Value, out var value))
                return value;

            current = parents.GetValueOrDefault(current.Value);
        }

        return global;
    }

    private static bool IsTrue(string? value) {
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }
}
