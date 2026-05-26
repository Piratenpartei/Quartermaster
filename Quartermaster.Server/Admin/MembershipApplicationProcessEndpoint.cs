using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Admin;

public class MembershipApplicationProcessRequest {
    public Guid Id { get; set; }
    public ApplicationStatus Status { get; set; }
}

public class MembershipApplicationProcessEndpoint : Endpoint<MembershipApplicationProcessRequest> {
    private readonly MembershipApplicationRepository _applicationRepo;
    private readonly PermissionContext _perms;

    public MembershipApplicationProcessEndpoint(MembershipApplicationRepository applicationRepo,
        PermissionContext perms) {
        _applicationRepo = applicationRepo;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/admin/membershipapplications/process");
    }

    public override async Task HandleAsync(MembershipApplicationProcessRequest req, CancellationToken ct) {
        var application = _applicationRepo.Get(req.Id);
        if (application == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        if (application.ChapterId.HasValue) {
            if (!_perms.Has(application.ChapterId.Value, PermissionIdentifier.ProcessApplications)) {
                await SendForbiddenAsync(ct);
                return;
            }
        } else {
            if (!_perms.HasGlobal(PermissionIdentifier.ProcessApplications)) {
                await SendForbiddenAsync(ct);
                return;
            }
        }

        if (req.Status != ApplicationStatus.Approved && req.Status != ApplicationStatus.Rejected) {
            await SendErrorsAsync(400, ct);
            return;
        }

        _applicationRepo.UpdateStatus(req.Id, req.Status, null);
        await SendOkAsync(ct);
    }
}
