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

    public DueSelectionDetailEndpoint(DueSelectionRepository dueSelectionRepo, MotionRepository motionRepo,
        MembershipApplicationRepository applicationRepo, PermissionContext perms) {
        _dueSelectionRepo = dueSelectionRepo;
        _motionRepo = motionRepo;
        _applicationRepo = applicationRepo;
        _perms = perms;
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
        if (application?.ChapterId.HasValue == true) {
            if (!_perms.Has(application.ChapterId.Value, PermissionIdentifier.ViewDueSelections)) {
                await SendForbiddenAsync(ct);
                return;
            }
        } else {
            if (!_perms.HasGlobal(PermissionIdentifier.ViewDueSelections)) {
                await SendForbiddenAsync(ct);
                return;
            }
        }

        var dto = ds.ToDetailDto();
        dto.LinkedMotionId = _motionRepo.GetByLinkedDueSelectionId(ds.Id)?.Id;
        await SendAsync(dto, cancellation: ct);
    }
}
