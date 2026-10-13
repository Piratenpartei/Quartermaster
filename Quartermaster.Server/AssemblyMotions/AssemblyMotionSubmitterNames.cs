using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Data;
using Quartermaster.Data.AssemblyMotions;

namespace Quartermaster.Server.AssemblyMotions;

public static class AssemblyMotionSubmitterNames {
    /// <summary>Display names per motion id, author first, then by join time.</summary>
    public static Dictionary<Guid, List<string>> ForMotions(DbContext db, AssemblyMotionRepository motionRepository, IReadOnlyCollection<AssemblyMotion> motions) {
        var submittersByMotion = motionRepository.GetSubmitters(motions.Select(m => m.Id).ToList());
        var memberIds = submittersByMotion.Values
            .SelectMany(list => list)
            .Select(s => s.MemberId)
            .Distinct()
            .ToList();
        var members = db.Members
            .Where(m => memberIds.Contains(m.Id))
            .ToList()
            .ToDictionary(m => m.Id);

        return motions.ToDictionary(
            m => m.Id,
            m => submittersByMotion.GetValueOrDefault(m.Id, [])
                .OrderByDescending(s => s.IsAuthor)
                .ThenBy(s => s.JoinedAt)
                .Select(s => AssemblyMotionDetailBuilder.DisplayName(members.GetValueOrDefault(s.MemberId)))
                .ToList());
    }
}
