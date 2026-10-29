using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.ApplicantRegister;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Data.ApplicantRegister;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Motions;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.Chapters;
using Quartermaster.Server.Forwarding.SageEntry;

namespace Quartermaster.Server.Admin;

public class MembershipApplicationDetailRequest {
    public Guid Id { get; set; }
}

public class MembershipApplicationDetailEndpoint
    : Endpoint<MembershipApplicationDetailRequest, MembershipApplicationDetailDTO> {

    private readonly MembershipApplicationRepository _applicationRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly DueSelectionRepository _dueSelectionRepo;
    private readonly MotionRepository _motionRepo;
    private readonly SageEntryGate _gate;
    private readonly ApplicantRegisterRepository _register;
    private readonly TakeOverPlanner _planner;
    private readonly PermissionContext _perms;

    public MembershipApplicationDetailEndpoint(
        MembershipApplicationRepository applicationRepo,
        ChapterRepository chapterRepo,
        DueSelectionRepository dueSelectionRepo,
        MotionRepository motionRepo,
        SageEntryGate gate,
        ApplicantRegisterRepository register,
        TakeOverPlanner planner,
        PermissionContext perms) {
        _applicationRepo = applicationRepo;
        _chapterRepo = chapterRepo;
        _dueSelectionRepo = dueSelectionRepo;
        _motionRepo = motionRepo;
        _gate = gate;
        _register = register;
        _planner = planner;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/admin/membershipapplications/{Id}");
    }

    public override async Task HandleAsync(MembershipApplicationDetailRequest req, CancellationToken ct) {
        var app = _applicationRepo.Get(req.Id);
        if (app == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        if (app.ChapterId.HasValue) {
            if (!_perms.Has(app.ChapterId.Value, PermissionIdentifier.ViewApplications)) {
                await SendForbiddenAsync(ct);
                return;
            }
        } else {
            if (!_perms.HasGlobal(PermissionIdentifier.ViewApplications)) {
                await SendForbiddenAsync(ct);
                return;
            }
        }

        var chapter = app.ChapterId.HasValue ? _chapterRepo.Get(app.ChapterId.Value) : null;
        var dueSelection = app.DueSelectionId.HasValue ? _dueSelectionRepo.Get(app.DueSelectionId.Value) : null;
        var isReduced = dueSelection != null && dueSelection.SelectedValuation == SelectedValuation.Reduced;

        var dto = app.ToDetailDto(chapter?.Name ?? "", isReduced);
        dto.DueSelection = dueSelection?.ToAdminDto();
        var pendingMotion = _motionRepo.GetByLinkedApplicationId(app.Id);
        dto.LinkedMotionId = pendingMotion?.Id;
        if (pendingMotion != null) {
            var next = _planner.NextFor(pendingMotion, app, DateTime.UtcNow);
            dto.NextTakeOverAt = next?.DueAt.ToDtoUtc();
            dto.NextTakeOverChapterName = next?.ChapterName;
        }

        var approvalMail = _gate.StatusOf(app.Id);
        dto.ApprovalMailState = approvalMail.State;
        dto.ApprovalMailSentAt = approvalMail.SentAt.ToDtoUtc();
        dto.FederalTicketUrl = approvalMail.FederalTicketUrl;
        if (_perms.HasGlobal(PermissionIdentifier.ManageApplicantRegister))
            dto.RegisterHits = RegisterHitsOf(app.Id);
        await SendAsync(dto, cancellation: ct);
    }

    private List<ApplicationRegisterHitDTO> RegisterHitsOf(Guid applicationId) {
        var hits = _register.HitsFor(applicationId);
        var entries = _register.GetMany(hits.Select(h => h.EntryId).Distinct().ToList());
        var dtos = new List<ApplicationRegisterHitDTO>();
        foreach (var hit in hits) {
            if (entries.TryGetValue(hit.EntryId, out var entry))
                dtos.Add(hit.ToDto(entry));
        }
        return dtos;
    }
}
