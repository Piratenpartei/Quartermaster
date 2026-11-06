using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.DueSelector;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Motions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Admin;

public class DueSelectionDetailRequest {
    public Guid Id { get; set; }
}

public class DueSelectionDetailEndpoint
    : Endpoint<DueSelectionDetailRequest, DueSelectionDetailDTO> {

    private readonly DueSelectionRepository _dueSelectionRepo;
    private readonly MotionRepository _motionRepo;
    private readonly MembershipApplicationRepository _applicationRepo;
    private readonly PermissionContext _perms;
    private readonly DetailPageAccess _pageAccess;

    public DueSelectionDetailEndpoint(DueSelectionRepository dueSelectionRepo, MotionRepository motionRepo,
        MembershipApplicationRepository applicationRepo, PermissionContext perms, DetailPageAccess pageAccess) {
        _dueSelectionRepo = dueSelectionRepo;
        _motionRepo = motionRepo;
        _applicationRepo = applicationRepo;
        _perms = perms;
        _pageAccess = pageAccess;
    }

    public override void Configure() {
        Get("/api/admin/dueselections/{Id}");
    }

    public override async Task HandleAsync(DueSelectionDetailRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var ds = _dueSelectionRepo.Get(req.Id);
        if (ds == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var application = _applicationRepo.GetByDueSelectionId(ds.Id);
        if (!_pageAccess.CanViewDueSelection(ds, application)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var dto = ds.ToDetailDto();
        var motion = _motionRepo.GetByLinkedDueSelectionId(ds.Id);
        if (motion != null && _pageAccess.CanViewMotion(motion))
            dto.ViewableMotionId = motion.Id;
        if (application != null && _pageAccess.CanViewApplication(application))
            dto.ViewableApplicationId = application.Id;
        await SendAsync(dto, cancellation: ct);
    }
}
