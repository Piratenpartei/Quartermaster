using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Quartermaster.Api.Forwarding;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Options;
using Quartermaster.Server.Forwarding.SageEntry;

namespace Quartermaster.Server.PublicForms;

/// <summary>Reads the switches for the public membership application, due selection and motion forms, and the limits of the dues form.</summary>
public class PublicFormSettings {
    private readonly OptionRepository _options;
    private readonly ChapterRepository _chapters;
    private readonly SageEntrySetup _sageSetup;

    public PublicFormSettings(OptionRepository options, ChapterRepository chapters, SageEntrySetup sageSetup) {
        _options = options;
        _chapters = chapters;
        _sageSetup = sageSetup;
    }

    /// <summary>The public form switch is on and the SAGE entry setup is complete.</summary>
    public bool IsMembershipApplicationEnabled() {
        return IsTrue(_options.GetGlobalValue(PublicFormOptions.MembershipApplication)?.Value) && IsMembershipApplicationReady();
    }

    /// <summary>Applications are only accepted, by anyone, once the federal Redmine setup carries everything the SAGE entry check needs.</summary>
    public bool IsMembershipApplicationReady() {
        return _sageSetup.IsComplete();
    }

    public bool IsDueSelectionEnabled() {
        return IsTrue(_options.GetGlobalValue(PublicFormOptions.DueSelection)?.Value);
    }

    public bool IsPermanentReductionEnabled() {
        return IsTrue(_options.GetGlobalValue(PublicFormOptions.PermanentReduction)?.Value);
    }

    /// <summary>The regular minimum: from this yearly amount on no decision is needed, so a reduction can ask for at most this much.</summary>
    public decimal ReducedDueMaximum() {
        var value = _options.GetGlobalValue(ForwardingOptionKeys.RegularMinimum)?.Value?.Trim().Replace(',', '.');
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) && amount > 0)
            return amount;

        return ForwardingOptionKeys.DefaultRegularMinimum;
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
