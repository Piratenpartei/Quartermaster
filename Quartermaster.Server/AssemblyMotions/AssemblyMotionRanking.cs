using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Data.AssemblyMotions;

namespace Quartermaster.Server.AssemblyMotions;

public record AssemblyMotionRankingEntry(AssemblyMotion Motion, AssemblyMotionType Type, int UpvoteCount, int Rank);

public static class AssemblyMotionRanking {
    public static List<AssemblyMotionRankingEntry> Compute(IEnumerable<AssemblyMotion> motions,
        IReadOnlyDictionary<Guid, AssemblyMotionType> types, IReadOnlyDictionary<Guid, int> counts) {
        var ordered = motions
            .Where(AssemblyMotionUpvoteService.CanReceiveUpvotes)
            .Select(m => new {
                Motion = m,
                Type = types.TryGetValue(m.TypeId, out var type)
                    ? type
                    : throw new InvalidOperationException($"Type {m.TypeId} not found for motion {m.Id}."),
                Count = counts.GetValueOrDefault(m.Id)
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Motion.SubmittedAt)
            .ThenBy(x => x.Type.SortOrder)
            .ThenBy(x => x.Motion.Number)
            .ThenBy(x => x.Motion.Id)
            .ToList();

        return ordered
            .Select((x, index) => new AssemblyMotionRankingEntry(x.Motion, x.Type, x.Count, index + 1))
            .ToList();
    }
}
