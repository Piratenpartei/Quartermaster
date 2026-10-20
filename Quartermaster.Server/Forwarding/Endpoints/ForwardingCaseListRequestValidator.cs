using FastEndpoints;
using Quartermaster.Api.Forwarding;
using Quartermaster.Server.Validation;

namespace Quartermaster.Server.Forwarding.Endpoints;

public class ForwardingCaseListRequestValidator : Validator<ForwardingCaseListRequest> {
    public ForwardingCaseListRequestValidator() {
        this.AddPaginationRules();
    }
}
