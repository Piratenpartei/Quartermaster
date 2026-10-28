using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.ApplicantRegister;
using Quartermaster.Data.ApplicantRegister;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.ApplicantRegister.Endpoints;

public class RegisterEntryListEndpoint : Endpoint<ApplicantRegisterEntrySearchRequest, ApplicantRegisterEntrySearchResponse> {
    private readonly ApplicantRegisterRepository _register;
    private readonly ApplicantRegisterEntryMapper _mapper;
    private readonly PermissionContext _perms;

    public RegisterEntryListEndpoint(ApplicantRegisterRepository register, ApplicantRegisterEntryMapper mapper, PermissionContext perms) {
        _register = register;
        _mapper = mapper;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/admin/applicant-register");
    }

    public override async Task HandleAsync(ApplicantRegisterEntrySearchRequest req, CancellationToken ct) {
        if (!_perms.HasGlobal(PermissionIdentifier.ManageApplicantRegister)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var (items, totalCount) = _register.Search(req.Query, req.Page, req.PageSize);

        await SendAsync(new ApplicantRegisterEntrySearchResponse {
            Items = _mapper.ToDtos(items),
            TotalCount = totalCount,
            Page = req.Page,
            PageSize = req.PageSize
        }, cancellation: ct);
    }
}
