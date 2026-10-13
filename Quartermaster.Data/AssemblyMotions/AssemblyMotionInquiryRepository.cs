using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;

namespace Quartermaster.Data.AssemblyMotions;

public class AssemblyMotionInquiryRepository {
    private readonly DbContext _context;

    public AssemblyMotionInquiryRepository(DbContext context) {
        _context = context;
    }

    public AssemblyMotionInquiry? Get(Guid id) {
        return _context.AssemblyMotionInquiries.Where(i => i.Id == id).FirstOrDefault();
    }

    public List<AssemblyMotionInquiry> ListForMotion(Guid motionId) {
        return _context.AssemblyMotionInquiries
                .Where(i => i.MotionId == motionId)
                .OrderBy(i => i.AskedAt)
                .ToList();
    }

    public Dictionary<Guid, int> CountOpen(IReadOnlyCollection<Guid> motionIds) {
        return _context.AssemblyMotionInquiries
            .Where(i => motionIds.Contains(i.MotionId) && i.ClosedAt == null)
            .GroupBy(i => i.MotionId)
            .Select(g => new { MotionId = g.Key, Count = g.Count() })
            .ToList()
            .ToDictionary(x => x.MotionId, x => x.Count);
    }

    public bool HasOpen(Guid motionId) {
        return _context.AssemblyMotionInquiries.Any(i => i.MotionId == motionId && i.ClosedAt == null);
    }

    public void Insert(AssemblyMotionInquiry inquiry) {
        _context.Insert(inquiry);
    }

    public void Update(AssemblyMotionInquiry inquiry) {
        _context.AssemblyMotionInquiries
            .Where(i => i.Id == inquiry.Id)
            .Set(i => i.AnswerMarkdown, inquiry.AnswerMarkdown)
            .Set(i => i.AnswerHtml, inquiry.AnswerHtml)
            .Set(i => i.AnsweredByMemberId, inquiry.AnsweredByMemberId)
            .Set(i => i.AnsweredAt, inquiry.AnsweredAt)
            .Set(i => i.ClosedAt, inquiry.ClosedAt)
            .Set(i => i.ClosedByUserId, inquiry.ClosedByUserId)
            .Update();
    }
}
