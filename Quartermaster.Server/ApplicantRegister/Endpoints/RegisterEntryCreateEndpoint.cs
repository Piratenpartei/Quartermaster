using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.ApplicantRegister;
using Quartermaster.Data.ApplicantRegister;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.ApplicantRegister.Endpoints;

public class RegisterEntryCreateEndpoint : Endpoint<ApplicantRegisterEntrySaveRequest, ApplicantRegisterEntryDTO> {
    private readonly ApplicantRegisterRepository _register;
    private readonly ApplicantRegisterEntryMapper _mapper;
    private readonly PermissionContext _perms;

    public RegisterEntryCreateEndpoint(ApplicantRegisterRepository register, ApplicantRegisterEntryMapper mapper, PermissionContext perms) {
        _register = register;
        _mapper = mapper;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/admin/applicant-register");
    }

    public override async Task HandleAsync(ApplicantRegisterEntrySaveRequest req, CancellationToken ct) {
        if (!_perms.HasGlobal(PermissionIdentifier.ManageApplicantRegister)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var entry = new ApplicantRegisterEntry {
            SourceApplicationId = null,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = _perms.UserId
        };
        ApplicantRegisterEntryMapper.ApplyEditableFields(entry, req);
        _register.Create(entry);

        await SendAsync(_mapper.ToDto(entry), cancellation: ct);
    }
}
