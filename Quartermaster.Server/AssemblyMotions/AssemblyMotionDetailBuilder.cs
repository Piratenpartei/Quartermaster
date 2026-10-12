using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Api;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;
using Quartermaster.Data.Members;

namespace Quartermaster.Server.AssemblyMotions;

public class AssemblyMotionDetailBuilder {
    public const string FormerMemberName = "Ehemaliges Mitglied";

    private readonly AssemblyMotionRepository _motions;
    private readonly AssemblyMotionService _service;
    private readonly AssemblyMotionAccess _access;
    private readonly DbContext _db;

    public AssemblyMotionDetailBuilder(AssemblyMotionRepository motions, AssemblyMotionService service,
        AssemblyMotionAccess access, DbContext db) {
        _motions = motions;
        _service = service;
        _access = access;
        _db = db;
    }

    public bool CanSee(AssemblyMotion motion, Event ev, EligibilityResult eligibility) {
        return AssemblyMotionVisibility.CanSee(motion, CreateViewer(ev, eligibility));
    }

    /// <summary>Returns null when the viewer may not see the motion.</summary>
    public AssemblyMotionDetailDTO? Build(AssemblyMotion motion, AssemblyMotionPortal portal, AssemblyMotionType type,
        Event ev, EligibilityResult eligibility, DateTime nowUtc) {
        var submitters = _motions.GetSubmitters(motion.Id);
        var viewerMember = eligibility.Member;
        var viewerIsSubmitter = IsSubmitter(submitters, viewerMember);
        var viewer = CreateViewer(ev, eligibility);
        if (!AssemblyMotionVisibility.CanSee(motion, viewer))
            return null;

        var dto = AssemblyMotionPublicView.Fill(new AssemblyMotionDetailDTO(), motion, type);
        dto.PortalId = portal.Id;
        dto.EventId = portal.EventId;
        dto.TextMarkdown = motion.TextMarkdown;
        dto.ReasonMarkdown = motion.ReasonMarkdown;
        dto.Revision = motion.Revision;
        dto.IsCompetingMotion = motion.IsCompetingMotion;
        dto.MinSubmitters = type.MinSubmitters;
        dto.EffectiveDeadline = AssemblyMotionDeadlines.Effective(portal, type, motion.IsCompetingMotion).ToDtoUtc();
        dto.IsAuthor = viewerMember != null && motion.CreatedByMemberId == viewerMember.Id;
        dto.IsSubmitter = viewerIsSubmitter;

        if (AssemblyMotionVisibility.CanSeeNames(viewer, viewerIsSubmitter))
            dto.Submitters = BuildSubmitters(submitters, viewer.IsCommission);

        if (viewerMember != null)
            ApplyActionFlags(dto, motion, portal, type, viewerMember, eligibility.IsEligible, viewerIsSubmitter, nowUtc);

        return dto;
    }

    private void ApplyActionFlags(AssemblyMotionDetailDTO dto, AssemblyMotion motion, AssemblyMotionPortal portal,
        AssemblyMotionType type, Member viewerMember, bool isEligible, bool viewerIsSubmitter, DateTime nowUtc) {
        dto.CanEdit = isEligible && _service.CanEdit(motion, viewerMember);
        dto.CanRelease = isEligible && _service.CanRelease(portal, type, motion, viewerMember, nowUtc);
        dto.CanJoin = isEligible && _service.CanJoin(portal, type, motion, viewerIsSubmitter, nowUtc);
        dto.CanDelete = _service.CanDelete(motion, viewerMember);
        dto.CanLeave = _service.CanLeave(motion, viewerMember, viewerIsSubmitter);
        dto.CanWithdraw = _service.CanWithdraw(portal, motion, viewerMember, nowUtc);
    }

    private List<AssemblyMotionSubmitterDTO> BuildSubmitters(List<AssemblyMotionSubmitter> submitters, bool includeMemberNumbers) {
        var memberIds = submitters
            .Select(s => s.MemberId)
            .ToList();
        var members = _db.Members
            .Where(m => memberIds.Contains(m.Id))
            .ToList()
            .ToDictionary(m => m.Id);

        return submitters
            .Select(s => {
                members.TryGetValue(s.MemberId, out var member);
                return new AssemblyMotionSubmitterDTO {
                    MemberId = s.MemberId,
                    DisplayName = DisplayName(member),
                    IsAuthor = s.IsAuthor,
                    JoinedAt = s.JoinedAt.ToDtoUtc(),
                    JoinedRevision = s.JoinedRevision,
                    MemberNumber = includeMemberNumbers ? member?.MemberNumber : null
                };
            })
            .ToList();
    }

    private static string DisplayName(Member? member) {
        if (member == null || member.AnonymizedAt != null)
            return FormerMemberName;
        return $"{member.FirstName} {member.LastName}";
    }

    private static bool IsSubmitter(List<AssemblyMotionSubmitter> submitters, Member? member)
        => member != null && submitters.Any(s => s.MemberId == member.Id);

    private AssemblyMotionViewer CreateViewer(Event ev, EligibilityResult eligibility)
        => new(eligibility.Member?.Id, eligibility.IsEligible, _access.IsCommission(ev));
}
