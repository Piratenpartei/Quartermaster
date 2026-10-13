using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Api;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;
using Quartermaster.Data.Members;
using Quartermaster.Server.AssemblyMotions.Wiki;

namespace Quartermaster.Server.AssemblyMotions;

public class AssemblyMotionDetailBuilder {
    public const string FormerMemberName = "Ehemaliges Mitglied";

    private readonly AssemblyMotionRepository _motions;
    private readonly AssemblyMotionInquiryRepository _inquiries;
    private readonly AssemblyMotionUpvoteRepository _upvotes;
    private readonly AssemblyMotionService _service;
    private readonly AssemblyMotionCompetitionViewBuilder _competitionViews;
    private readonly AssemblyMotionAccess _access;
    private readonly DbContext _db;

    public AssemblyMotionDetailBuilder(AssemblyMotionRepository motions, AssemblyMotionInquiryRepository inquiries,
        AssemblyMotionUpvoteRepository upvotes, AssemblyMotionService service, AssemblyMotionCompetitionViewBuilder competitionViews, AssemblyMotionAccess access, DbContext db) {
        _motions = motions;
        _inquiries = inquiries;
        _upvotes = upvotes;
        _service = service;
        _competitionViews = competitionViews;
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
        dto.Competitions = _competitionViews.BuildPublic(motion);
        dto.CompetitionLinks = _competitionViews.BuildDetail(motion, viewerMember, viewer.IsCommission);

        if (AssemblyMotionVisibility.CanSeeNames(viewer, viewerIsSubmitter))
            dto.Submitters = BuildSubmitters(submitters, viewer.IsCommission);

        if (viewer.IsCommission)
            dto.Commission = BuildCommissionInfo(motion, portal, type);

        if (viewerIsSubmitter || viewer.IsCommission)
            ApplyInquiries(dto, motion);

        if (AssemblyMotionUpvoteService.IsCountPublic(portal, motion, nowUtc))
            dto.UpvoteCount = _upvotes.CountForMotion(motion.Id);

        if (viewerMember != null)
            ApplyActionFlags(dto, motion, portal, type, viewerMember, eligibility.IsEligible, viewerIsSubmitter, nowUtc);

        return dto;
    }

    private void ApplyInquiries(AssemblyMotionDetailDTO dto, AssemblyMotion motion) {
        var inquiries = _inquiries.ListForMotion(motion.Id);
        var answererIds = inquiries
            .Where(i => i.AnsweredByMemberId != null)
            .Select(i => i.AnsweredByMemberId!.Value)
            .Distinct()
            .ToList();
        var answerers = _db.Members
            .Where(m => answererIds.Contains(m.Id))
            .ToList()
            .ToDictionary(m => m.Id);

        dto.OpenInquiryCount = inquiries.Count(i => i.ClosedAt == null);
        dto.Inquiries = inquiries
            .Select(i => new AssemblyMotionInquiryDTO {
                Id = i.Id,
                QuestionHtml = i.QuestionHtml,
                AskedAt = i.AskedAt.ToDtoUtc(),
                ResponseDueAt = i.ResponseDueAt.ToDtoUtc(),
                AnswerHtml = i.AnswerHtml,
                AnsweredAt = i.AnsweredAt.ToDtoUtc(),
                AnsweredByName = i.AnsweredByMemberId == null ? null : DisplayName(answerers.GetValueOrDefault(i.AnsweredByMemberId.Value)),
                ClosedAt = i.ClosedAt.ToDtoUtc()
            })
            .ToList();
    }

    private void ApplyActionFlags(AssemblyMotionDetailDTO dto, AssemblyMotion motion, AssemblyMotionPortal portal,
        AssemblyMotionType type, Member viewerMember, bool isEligible, bool viewerIsSubmitter, DateTime nowUtc) {
        dto.CanEdit = isEligible && _service.CanEditAsAuthor(motion, type, portal, viewerMember.Id, nowUtc);
        dto.CanRelease = isEligible && _service.CanRelease(portal, type, motion, viewerMember, nowUtc);
        dto.CanJoin = isEligible && _service.CanJoin(portal, type, motion, viewerIsSubmitter, nowUtc);
        dto.CanDelete = _service.CanDelete(motion, viewerMember);
        dto.CanLeave = _service.CanLeave(motion, viewerMember, viewerIsSubmitter);
        dto.CanWithdraw = _service.CanWithdraw(portal, motion, viewerMember, nowUtc);
        dto.CanUpvote = AssemblyMotionUpvoteService.CanUpvote(portal, motion, isEligible, nowUtc);
        dto.HasUpvoted = _upvotes.Exists(motion.Id, viewerMember.Id);
    }

    private AssemblyMotionCommissionInfoDTO BuildCommissionInfo(AssemblyMotion motion, AssemblyMotionPortal portal, AssemblyMotionType type) {
        return new() {
            CheckedAt = motion.CheckedAt.ToDtoUtc(),
            CheckedByName = CheckedByName(motion.CheckedByUserId),
            WikiMirrorEnabled = portal.WikiMirrorEnabled,
            WikiPageTitle = WikiPageTitle(motion, portal, type),
            WikiSyncState = motion.WikiSyncState,
            WikiSyncedRevision = motion.WikiSyncedRevision,
            WikiLastSyncedAt = motion.WikiLastSyncedAt.ToDtoUtc(),
            WikiLastError = motion.WikiLastError,
            OpenSlidesMotionId = motion.OpenSlidesMotionId,
            OpenSlidesPushedRevision = motion.OpenSlidesPushedRevision,
            OpenSlidesPushedAt = motion.OpenSlidesPushedAt.ToDtoUtc(),
            OpenSlidesLastError = motion.OpenSlidesLastError
        };
    }

    private static string? WikiPageTitle(AssemblyMotion motion, AssemblyMotionPortal portal, AssemblyMotionType type) {
        if (motion.Number == null)
            return null;
        if (string.IsNullOrWhiteSpace(portal.WikiPagePrefix))
            return null;

        return AssemblyMotionWikiPage.Title(portal, type, motion);
    }

    private string? CheckedByName(Guid? userId) {
        if (userId == null)
            return null;

        var user = _db.Users
            .Where(u => u.Id == userId.Value)
            .FirstOrDefault();
        if (user == null)
            return null;

        var name = $"{user.FirstName} {user.LastName}".Trim();
        return name.Length > 0 ? name : user.Username;
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
                    DisplayName = DisplayName(member),
                    IsAuthor = s.IsAuthor,
                    JoinedAt = s.JoinedAt.ToDtoUtc(),
                    JoinedRevision = s.JoinedRevision,
                    MemberNumber = includeMemberNumbers ? member?.MemberNumber : null
                };
            })
            .ToList();
    }

    public static string DisplayName(Member? member) {
        if (member == null || member.AnonymizedAt != null)
            return FormerMemberName;
        return $"{member.FirstName} {member.LastName}";
    }

    private static bool IsSubmitter(List<AssemblyMotionSubmitter> submitters, Member? member) {
        return member != null && submitters.Any(s => s.MemberId == member.Id);
    }

    private AssemblyMotionViewer CreateViewer(Event ev, EligibilityResult eligibility) {
        return new(eligibility.Member?.Id, eligibility.IsEligible, _access.IsCommission(ev));
    }
}
