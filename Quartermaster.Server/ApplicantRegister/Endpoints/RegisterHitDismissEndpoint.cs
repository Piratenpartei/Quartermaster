using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Data.ApplicantRegister;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.ApplicantRegister.Endpoints;

public class RegisterHitDismissRequest {
    public Guid ApplicationId { get; set; }
    public Guid HitId { get; set; }
}

public class RegisterHitDismissEndpoint : Endpoint<RegisterHitDismissRequest> {
    private readonly MembershipApplicationRepository _applications;
    private readonly ApplicantRegisterRepository _register;
    private readonly FederalApprovalFlow _flow;
    private readonly PermissionContext _perms;

    public RegisterHitDismissEndpoint(MembershipApplicationRepository applications, ApplicantRegisterRepository register,
        FederalApprovalFlow flow, PermissionContext perms) {
        _applications = applications;
        _register = register;
        _flow = flow;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/admin/membershipapplications/{ApplicationId}/register-hits/{HitId}/dismiss");
    }

    public override async Task HandleAsync(RegisterHitDismissRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ManageApplicantRegister)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var application = _applications.Get(req.ApplicationId);
        var hit = application == null ? null : _register.HitsFor(req.ApplicationId).FirstOrDefault(h => h.Id == req.HitId);
        if (application == null || hit == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var result = hit.DismissedAt == null
            ? await _flow.DismissAsync(req.ApplicationId, req.HitId, _perms.UserId.Value, ct)
            : RegisterHitDismissal.AlreadyDismissed;
        if (result == RegisterHitDismissal.AlreadyDismissed) {
            AddError(I18nKey.Error.Admin.Application.RegisterHitAlreadyDismissed);
            await SendErrorsAsync(409, ct);
            return;
        }
        if (result == RegisterHitDismissal.StatusChanged) {
            AddError(I18nKey.Error.Admin.Application.AlreadyDecided);
            await SendErrorsAsync(409, ct);
            return;
        }

        await SendOkAsync(ct);
    }
}
