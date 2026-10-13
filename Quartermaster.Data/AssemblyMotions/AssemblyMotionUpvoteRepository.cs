using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;

namespace Quartermaster.Data.AssemblyMotions;

public class AssemblyMotionUpvoteRepository {
    private readonly DbContext _context;

    public AssemblyMotionUpvoteRepository(DbContext context) {
        _context = context;
    }

    public bool Exists(Guid motionId, Guid memberId) {
        return _context.AssemblyMotionUpvotes.Any(u => u.MotionId == motionId && u.MemberId == memberId);
    }

    public void Insert(AssemblyMotionUpvote upvote) {
        _context.Insert(upvote);
    }

    public void Delete(Guid motionId, Guid memberId) {
        _context.AssemblyMotionUpvotes
                .Where(u => u.MotionId == motionId && u.MemberId == memberId)
                .Delete();
    }

    public List<Guid> MotionIdsForMember(Guid portalId, Guid memberId) {
        return _context.AssemblyMotionUpvotes
                .InnerJoin(_context.AssemblyMotions, (u, m) => u.MotionId == m.Id, (u, m) => new { u.MemberId, m.PortalId, m.DeletedAt, u.MotionId })
                .Where(x => x.PortalId == portalId && x.MemberId == memberId && x.DeletedAt == null)
                .Select(x => x.MotionId)
                .ToList();
    }

    public Dictionary<Guid, int> CountsForPortal(Guid portalId) {
        return _context.AssemblyMotionUpvotes
                .InnerJoin(_context.AssemblyMotions, (u, m) => u.MotionId == m.Id, (u, m) => new { u.MotionId, m.PortalId, m.DeletedAt })
                .Where(x => x.PortalId == portalId && x.DeletedAt == null)
                .GroupBy(x => x.MotionId)
                .Select(g => new { MotionId = g.Key, Count = g.Count() })
                .ToList()
                .ToDictionary(x => x.MotionId, x => x.Count);
    }

    public int CountForMotion(Guid motionId) {
        return _context.AssemblyMotionUpvotes.Count(u => u.MotionId == motionId);
    }
}
