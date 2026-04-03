using FastEndpoints;
using Quartermaster.Api.AdministrativeDivisions;
using Quartermaster.Server.Validation;

namespace Quartermaster.Server.AdministrativeDivisions;

public class AdministrativeDivisionSearchRequestValidator : Validator<AdministrativeDivisionSearchRequest> {
    public AdministrativeDivisionSearchRequestValidator() {
        this.AddPaginationRules();
    }
}
