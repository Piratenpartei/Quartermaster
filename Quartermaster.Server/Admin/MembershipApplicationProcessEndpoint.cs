using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Data.MembershipApplications;

namespace Quartermaster.Server.Admin;

public class MembershipApplicationProcessRequest {
    public Guid Id { get; set; }
    public int Status { get; set; }
}

public class MembershipApplicationProcessEndpoint : Endpoint<MembershipApplicationProcessRequest> {
    private readonly MembershipApplicationRepository _applicationRepo;

    public MembershipApplicationProcessEndpoint(MembershipApplicationRepository applicationRepo) {
        _applicationRepo = applicationRepo;
    }

    public override void Configure() {
        Post("/api/admin/membershipapplications/process");
        AllowAnonymous(); // TODO: Replace with auth when login UI exists
    }

    public override async Task HandleAsync(MembershipApplicationProcessRequest req, CancellationToken ct) {
        var application = _applicationRepo.Get(req.Id);
        if (application == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var status = (ApplicationStatus)req.Status;
        if (status != ApplicationStatus.Approved && status != ApplicationStatus.Rejected) {
            await SendErrorsAsync(400, ct);
            return;
        }

        _applicationRepo.UpdateStatus(req.Id, status, null);
        await SendOkAsync(ct);
    }
}
