using FastEndpoints;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Server.Validation;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Commission;

public class AssemblyMotionDashboardRequestValidator : Validator<AssemblyMotionDashboardRequest> {
    public AssemblyMotionDashboardRequestValidator() {
        this.AddPaginationRules();
    }
}
