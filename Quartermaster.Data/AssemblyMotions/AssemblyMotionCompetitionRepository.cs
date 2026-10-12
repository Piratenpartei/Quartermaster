using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;

namespace Quartermaster.Data.AssemblyMotions;

public class AssemblyMotionCompetitionRepository {
    private readonly DbContext _context;

    public AssemblyMotionCompetitionRepository(DbContext context) {
        _context = context;
    }

    public List<AssemblyMotionCompetition> ListForMotion(Guid motionId)
        => _context.AssemblyMotionCompetitions
            .Where(c => c.MotionAId == motionId || c.MotionBId == motionId)
            .OrderBy(c => c.DeclaredAt)
            .ToList();

    public Dictionary<Guid, List<AssemblyMotionCompetition>> ListForMotions(IReadOnlyCollection<Guid> motionIds) {
        var links = _context.AssemblyMotionCompetitions
            .Where(c => motionIds.Contains(c.MotionAId) || motionIds.Contains(c.MotionBId))
            .OrderBy(c => c.DeclaredAt)
            .ToList();

        var result = motionIds.ToDictionary(id => id, _ => new List<AssemblyMotionCompetition>());
        foreach (var link in links) {
            if (result.TryGetValue(link.MotionAId, out var forA))
                forA.Add(link);
            if (result.TryGetValue(link.MotionBId, out var forB))
                forB.Add(link);
        }
        return result;
    }

    public AssemblyMotionCompetition? Get(Guid id)
        => _context.AssemblyMotionCompetitions.Where(c => c.Id == id).FirstOrDefault();

    public bool Exists(Guid motionAId, Guid motionBId)
        => _context.AssemblyMotionCompetitions.Any(c => c.MotionAId == motionAId && c.MotionBId == motionBId);

    public void Insert(AssemblyMotionCompetition competition) => _context.Insert(competition);

    public void Delete(Guid id)
        => _context.AssemblyMotionCompetitions
            .Where(c => c.Id == id)
            .Delete();
}
