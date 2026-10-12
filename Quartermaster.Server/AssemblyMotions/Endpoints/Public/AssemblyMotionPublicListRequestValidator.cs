using FastEndpoints;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Server.Validation;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Public;

public class AssemblyMotionPublicListRequestValidator : Validator<AssemblyMotionPublicListRequest> {
    public AssemblyMotionPublicListRequestValidator() {
        this.AddPaginationRules();
    }
}
