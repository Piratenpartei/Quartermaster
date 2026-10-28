using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.ApplicantRegister;
using Quartermaster.Api.I18n;
using Quartermaster.Data.ApplicantRegister;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.ApplicantRegister.Endpoints;

public class RegisterEntryDeleteEndpoint : Endpoint<ApplicantRegisterEntryIdRequest> {
    private readonly ApplicantRegisterRepository _register;
    private readonly PermissionContext _perms;

    public RegisterEntryDeleteEndpoint(ApplicantRegisterRepository register, PermissionContext perms) {
        _register = register;
        _perms = perms;
    }

    public override void Configure() {
        Delete("/api/admin/applicant-register/{Id}");
    }

    public override async Task HandleAsync(ApplicantRegisterEntryIdRequest req, CancellationToken ct) {
        if (!_perms.HasGlobal(PermissionIdentifier.ManageApplicantRegister)) {
            await SendForbiddenAsync(ct);
            return;
        }
        if (_register.Get(req.Id) == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (_register.HasOpenHitsOfUndecidedApplications(req.Id)) {
            AddError(I18nKey.Error.ApplicantRegister.EntryInUse);
            await SendErrorsAsync(409, ct);
            return;
        }

        _register.Delete(req.Id);
        await SendNoContentAsync(ct);
    }
}
