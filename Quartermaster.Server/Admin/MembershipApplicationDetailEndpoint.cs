using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Motions;
using Quartermaster.Server.Authentication;

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
    private readonly PermissionContext _perms;

    public MembershipApplicationDetailEndpoint(
        MembershipApplicationRepository applicationRepo,
        ChapterRepository chapterRepo,
        DueSelectionRepository dueSelectionRepo,
        MotionRepository motionRepo,
        PermissionContext perms) {
        _applicationRepo = applicationRepo;
        _chapterRepo = chapterRepo;
        _dueSelectionRepo = dueSelectionRepo;
        _motionRepo = motionRepo;
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
        dto.LinkedMotionId = _motionRepo.GetByLinkedApplicationId(app.Id)?.Id;
        await SendAsync(dto, cancellation: ct);
    }
}
