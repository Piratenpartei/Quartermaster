using FastEndpoints;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Server.Validation;

namespace Quartermaster.Server.Admin;

public class MembershipApplicationListRequestValidator : Validator<MembershipApplicationListRequest> {
    public MembershipApplicationListRequestValidator() {
        this.AddPaginationRules();
    }
}
