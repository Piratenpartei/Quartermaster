using FastEndpoints;
using Quartermaster.Api.Members;
using Quartermaster.Server.Validation;

namespace Quartermaster.Server.Members;

public class MemberSearchRequestValidator : Validator<MemberSearchRequest> {
    public MemberSearchRequestValidator() {
        this.AddPaginationRules();
    }
}
