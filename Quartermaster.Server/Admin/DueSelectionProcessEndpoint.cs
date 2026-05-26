using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.DueSelector;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Admin;

public class DueSelectionProcessRequest {
    public Guid Id { get; set; }
    public DueSelectionStatus Status { get; set; }
}

public class DueSelectionProcessEndpoint : Endpoint<DueSelectionProcessRequest> {
    private readonly DueSelectionRepository _dueSelectionRepo;
    private readonly MembershipApplicationRepository _applicationRepo;
    private readonly PermissionContext _perms;

    public DueSelectionProcessEndpoint(DueSelectionRepository dueSelectionRepo,
        MembershipApplicationRepository applicationRepo, PermissionContext perms) {
        _dueSelectionRepo = dueSelectionRepo;
        _applicationRepo = applicationRepo;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/admin/dueselections/process");
    }

    public override async Task HandleAsync(DueSelectionProcessRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var selection = _dueSelectionRepo.Get(req.Id);
        if (selection == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var application = _applicationRepo.GetByDueSelectionId(selection.Id);
        if (application?.ChapterId.HasValue == true) {
            if (!_perms.Has(application.ChapterId.Value, PermissionIdentifier.ProcessDueSelections)) {
                await SendForbiddenAsync(ct);
                return;
            }
        } else {
            if (!_perms.HasGlobal(PermissionIdentifier.ProcessDueSelections)) {
                await SendForbiddenAsync(ct);
                return;
            }
        }

        if (req.Status != DueSelectionStatus.Approved && req.Status != DueSelectionStatus.Rejected) {
            await SendErrorsAsync(400, ct);
            return;
        }

        _dueSelectionRepo.UpdateStatus(req.Id, req.Status, null);
        await SendOkAsync(ct);
    }
}
