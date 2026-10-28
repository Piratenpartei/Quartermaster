using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.ApplicantRegister;
using Quartermaster.Data.ApplicantRegister;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.ApplicantRegister.Endpoints;

public class RegisterEntryGetEndpoint : Endpoint<ApplicantRegisterEntryIdRequest, ApplicantRegisterEntryDTO> {
    private readonly ApplicantRegisterRepository _register;
    private readonly ApplicantRegisterEntryMapper _mapper;
    private readonly PermissionContext _perms;

    public RegisterEntryGetEndpoint(ApplicantRegisterRepository register, ApplicantRegisterEntryMapper mapper, PermissionContext perms) {
        _register = register;
        _mapper = mapper;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/admin/applicant-register/{Id}");
    }

    public override async Task HandleAsync(ApplicantRegisterEntryIdRequest req, CancellationToken ct) {
        if (!_perms.HasGlobal(PermissionIdentifier.ManageApplicantRegister)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var entry = _register.Get(req.Id);
        if (entry == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        await SendAsync(_mapper.ToDto(entry), cancellation: ct);
    }
}
