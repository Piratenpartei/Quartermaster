using FastEndpoints;
using Quartermaster.Api.ApplicantRegister;
using Quartermaster.Server.Validation;

namespace Quartermaster.Server.ApplicantRegister.Endpoints;

public class ApplicantRegisterEntrySearchRequestValidator : Validator<ApplicantRegisterEntrySearchRequest> {
    public ApplicantRegisterEntrySearchRequestValidator() {
        this.AddPaginationRules();
    }
}
