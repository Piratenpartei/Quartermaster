using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.I18n;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.Forwarding;
using Quartermaster.Server.MembershipApplications;

namespace Quartermaster.Server.Admin;

public class MembershipApplicationProcessRequest {
    public Guid Id { get; set; }
    public ApplicationStatus Status { get; set; }
}

public class MembershipApplicationProcessEndpoint : Endpoint<MembershipApplicationProcessRequest> {
    private readonly MembershipApplicationRepository _applicationRepo;
    private readonly MembershipApplicationMailService _mailService;
    private readonly ForwardingOutcomeHook _outcomeHook;
    private readonly PermissionContext _perms;

    public MembershipApplicationProcessEndpoint(MembershipApplicationRepository applicationRepo,
        MembershipApplicationMailService mailService, ForwardingOutcomeHook outcomeHook, PermissionContext perms) {
        _applicationRepo = applicationRepo;
        _mailService = mailService;
        _outcomeHook = outcomeHook;
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
        // A second decision would send the applicant a contradicting mail.
        if (application.Status != ApplicationStatus.Pending && application.Status != ApplicationStatus.PendingDivisionLinking) {
            AddError(I18nKey.Error.Admin.Application.AlreadyDecided);
            await SendErrorsAsync(409, ct);
            return;
        }

        _applicationRepo.UpdateStatus(req.Id, req.Status, null);
        _outcomeHook.OnRecordDecided(ForwardingCaseKind.Application, req.Id, DateTime.UtcNow);

        var updated = _applicationRepo.Get(req.Id);
        if (updated != null) {
            await _mailService.SendApplicationDecisionAsync(updated, ct);
        }

        await SendOkAsync(ct);
    }
}
