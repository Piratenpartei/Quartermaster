using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Data.AssemblyMotions;

namespace Quartermaster.Server.AssemblyMotions;

public record AssemblyMotionOrderedEntry(AssemblyMotion Motion, AssemblyMotionType Type, int? Rank, int? UpvoteCount);

public static class AssemblyMotionExportOrder {
    /// <summary>When the ranking is public, motions that can receive upvotes come first in rank order; all others, and every motion while the ranking is not public, follow by type sort order, then number. Rank and count are set only for ranked motions.</summary>
    public static List<AssemblyMotionOrderedEntry> Sort(AssemblyMotionPortal portal, IEnumerable<AssemblyMotion> motions,
        IReadOnlyDictionary<Guid, AssemblyMotionType> types, AssemblyMotionUpvoteRepository upvotes, DateTime nowUtc) {
        var all = motions.ToList();
        var result = new List<AssemblyMotionOrderedEntry>();

        if (AssemblyMotionUpvoteService.IsRankingPublic(portal, nowUtc)) {
            result.AddRange(AssemblyMotionRanking.Compute(all, types, upvotes.CountsForPortal(portal.Id))
                .Select(entry => new AssemblyMotionOrderedEntry(entry.Motion, entry.Type, entry.Rank, entry.UpvoteCount)));
        }

        var rankedIds = result
            .Select(entry => entry.Motion.Id)
            .ToHashSet();
        result.AddRange(all
            .Where(m => !rankedIds.Contains(m.Id))
            .OrderBy(m => types[m.TypeId].SortOrder)
            .ThenBy(m => m.Number)
            .Select(m => new AssemblyMotionOrderedEntry(m, types[m.TypeId], null, null)));

        return result;
    }
}
