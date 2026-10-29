using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Server.Chapters;

/// <summary>When an undecided application motion moves up to a higher chapter. <c>above</c> lists the chapters over the motion's chapter, nearest first, the root last.</summary>
public static class TakeOverSchedule {
    /// <summary>The highest candidate whose deadline has passed, or null.</summary>
    public static TakeOverStep? Due(IReadOnlyList<TakeOverCandidate> above, DateTime startUtc, DateTime nowUtc) {
        return Steps(above, startUtc)
            .Where(s => s.DueAt <= nowUtc)
            .LastOrDefault();
    }

    /// <summary>The candidate with the earliest deadline still ahead, or null.</summary>
    public static TakeOverStep? Next(IReadOnlyList<TakeOverCandidate> above, DateTime startUtc, DateTime nowUtc) {
        return Steps(above, startUtc)
            .Where(s => s.DueAt > nowUtc)
            .OrderBy(s => s.DueAt)
            .FirstOrDefault();
    }

    private static IEnumerable<TakeOverStep> Steps(IReadOnlyList<TakeOverCandidate> above, DateTime startUtc) {
        return above
            .Where(c => c.HandlesApplications && c.PeriodDays != null)
            .Select(c => new TakeOverStep(c.ChapterId, c.ChapterName, startUtc.AddDays(c.PeriodDays!.Value)));
    }
}
