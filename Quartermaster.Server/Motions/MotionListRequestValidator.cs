using FastEndpoints;
using Quartermaster.Api.Motions;
using Quartermaster.Server.Validation;

namespace Quartermaster.Server.Motions;

public class MotionListRequestValidator : Validator<MotionListRequest> {
    public MotionListRequestValidator() {
        this.AddPaginationRules();
    }
}
