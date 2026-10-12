using System;
using System.Linq;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Events;
using Quartermaster.Data.Members;

namespace Quartermaster.Server.AssemblyMotions;

public class AssemblyMotionEligibility {
    private readonly DbContext _db;
    private readonly ChapterRepository _chapters;

    public AssemblyMotionEligibility(DbContext db, ChapterRepository chapters) {
        _db = db;
        _chapters = chapters;
    }

    public EligibilityResult Check(Guid? userId, Event ev, DateTime nowUtc) {
        if (userId == null)
            return new EligibilityResult(EligibilityProblem.NotSignedIn, null);

        var member = _db.Members.FirstOrDefault(m => m.UserId == userId);
        if (member == null)
            return new EligibilityResult(EligibilityProblem.NoLinkedMember, null);
        if (member.AnonymizedAt != null)
            return new EligibilityResult(EligibilityProblem.Anonymized, member);
        if (member.IsFloating)
            return new EligibilityResult(EligibilityProblem.NoMemberNumber, member);
        if (member.IsPending)
            return new EligibilityResult(EligibilityProblem.Pending, member);
        if (member.IsExited(nowUtc))
            return new EligibilityResult(EligibilityProblem.Exited, member);
        if (!IsInEventChapter(member, ev))
            return new EligibilityResult(EligibilityProblem.OutsideChapter, member);
        return new EligibilityResult(EligibilityProblem.None, member);
    }

    private bool IsInEventChapter(Member member, Event ev) {
        if (member.ChapterId == null)
            return IsRootChapter(ev.ChapterId);
        return _chapters.GetDescendantIds(ev.ChapterId).Contains(member.ChapterId.Value);
    }

    private bool IsRootChapter(Guid chapterId) {
        var chapter = _chapters.Get(chapterId);
        return chapter != null && chapter.ParentChapterId == null;
    }
}
