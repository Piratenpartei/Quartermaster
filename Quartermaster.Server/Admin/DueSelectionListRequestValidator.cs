using FastEndpoints;
using Quartermaster.Api.DueSelector;
using Quartermaster.Server.Validation;

namespace Quartermaster.Server.Admin;

public class DueSelectionListRequestValidator : Validator<DueSelectionListRequest> {
    public DueSelectionListRequestValidator() {
        this.AddPaginationRules();
    }
}
