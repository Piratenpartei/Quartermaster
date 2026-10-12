using FastEndpoints;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Server.Validation;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Members;

public class AssemblyMotionCollectingListRequestValidator : Validator<AssemblyMotionCollectingListRequest> {
    public AssemblyMotionCollectingListRequestValidator() {
        this.AddPaginationRules();
    }
}
