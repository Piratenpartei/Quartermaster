using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Motions;
using Quartermaster.Data;
using Quartermaster.Data.ChapterAssociates;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Motions;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.Chapters;

namespace Quartermaster.Server.Motions;

public class MotionDetailRequest {
    public Guid Id { get; set; }
}

public class MotionDetailEndpoint : Endpoint<MotionDetailRequest, MotionDetailDTO> {
    private readonly MotionRepository _motionRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly ChapterOfficerRepository _officerRepo;
    private readonly MembershipApplicationRepository _applicationRepo;
    private readonly TakeOverPlanner _planner;
    private readonly DbContext _context;
    private readonly PermissionContext _perms;

    public MotionDetailEndpoint(MotionRepository motionRepo, ChapterRepository chapterRepo,
        ChapterOfficerRepository officerRepo, MembershipApplicationRepository applicationRepo, TakeOverPlanner planner, DbContext context,
        PermissionContext perms) {
        _motionRepo = motionRepo;
        _chapterRepo = chapterRepo;
        _officerRepo = officerRepo;
        _applicationRepo = applicationRepo;
        _planner = planner;
        _context = context;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/motions/{Id}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(MotionDetailRequest req, CancellationToken ct) {
        var motion = _motionRepo.Get(req.Id);
        if (motion == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (!motion.IsPublic) {
            if (_perms.UserId == null) {
                await SendNotFoundAsync(ct);
                return;
            }
            if (!_perms.Has(motion.ChapterId, PermissionIdentifier.ViewMotions)) {
                await SendNotFoundAsync(ct);
                return;
            }
        }

        var chapter = _chapterRepo.Get(motion.ChapterId);
        var officers = _officerRepo.GetForChapter(motion.ChapterId);
        var officerMemberIds = officers.Select(o => o.MemberId).ToList();
        var members = _context.Members.Where(m => officerMemberIds.Contains(m.Id)).ToList();
        var votes = _motionRepo.GetVotes(motion.Id);

        var officerDtos = officers.Select(o => {
            var member = members.FirstOrDefault(m => m.Id == o.MemberId);
            return new MotionVoteDTO {
                MemberId = o.MemberId,
                MemberName = member != null ? $"{member.FirstName} {member.LastName}" : "Unbekannt",
                OfficerRole = o.AssociateType.ToString()
            };
        }).ToList();

        var voteDtos = votes.Select(v => {
            var member = members.FirstOrDefault(m => m.Id == v.MemberId);
            var officer = officers.FirstOrDefault(o => o.MemberId == v.MemberId);
            return new MotionVoteDTO {
                MemberId = v.MemberId,
                MemberName = member != null ? $"{member.FirstName} {member.LastName}" : "Unbekannt",
                OfficerRole = officer != null ? officer.AssociateType.ToString() : "",
                Vote = v.Vote,
                VotedAt = v.VotedAt.ToDtoUtc(),
                CastByUserId = v.CastByUserId
            };
        }).ToList();

        var canEdit = _perms.UserId != null && _perms.Has(motion.ChapterId, PermissionIdentifier.EditMotions);

        var dto = motion.ToDetailDto(chapter?.Name ?? "");
        if (!canEdit)
            dto.TextMarkdown = null;
        dto.Votes = voteDtos;
        dto.Officers = officerDtos;
        dto.TotalOfficers = officers.Count;
        if (_perms.UserId != null)
            FillNotice(dto, motion);
        await SendAsync(dto, cancellation: ct);
    }

    private void FillNotice(MotionDetailDTO dto, Motion motion) {
        var targetChapterId = motion.HandedOverToMotionId == null
            ? null
            : _motionRepo.Get(motion.HandedOverToMotionId.Value)?.ChapterId;
        var names = _chapterRepo.GetNames(new[] { motion.OnBehalfOfChapterId, targetChapterId }.OfType<Guid>().ToList());
        dto.OnBehalfOfChapterId = motion.OnBehalfOfChapterId;
        dto.OnBehalfOfChapterName = names.GetValueOrDefault(motion.OnBehalfOfChapterId ?? Guid.Empty);
        dto.OnBehalfOfReason = motion.OnBehalfOfReason;
        dto.HandedOverToMotionId = motion.HandedOverToMotionId;
        dto.HandedOverToChapterName = names.GetValueOrDefault(targetChapterId ?? Guid.Empty);
        dto.HandoverReason = motion.HandoverReason;
        FillPredecessor(dto, motion);
        FillTakeOver(dto, motion);
    }

    private void FillPredecessor(MotionDetailDTO dto, Motion motion) {
        var predecessor = _motionRepo.GetHandedOverFrom(motion.Id);
        if (predecessor == null)
            return;

        dto.HandedOverFromChapterName = _chapterRepo.Get(predecessor.ChapterId)?.Name;
        dto.HandedOverFromReason = predecessor.HandoverReason;
        if (motion.LinkedMembershipApplicationId != null)
            dto.ApplicationSubmittedAt = _applicationRepo.Get(motion.LinkedMembershipApplicationId.Value)?.SubmittedAt.ToDtoUtc();
    }

    private void FillTakeOver(MotionDetailDTO dto, Motion motion) {
        var rootId = _planner.TakeOverRootFor(motion);
        dto.CanTakeOver = rootId != null && _perms.Has(rootId.Value, PermissionIdentifier.ProcessDueSelections);
        if (motion.LinkedMembershipApplicationId == null)
            return;

        var application = _applicationRepo.Get(motion.LinkedMembershipApplicationId.Value);
        if (application == null)
            return;

        var next = _planner.NextFor(motion, application, DateTime.UtcNow);
        dto.NextTakeOverAt = next?.DueAt.ToDtoUtc();
        dto.NextTakeOverChapterName = next?.ChapterName;
    }
}
