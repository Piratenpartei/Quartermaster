using FastEndpoints;
using Quartermaster.Api.ChapterAssociates;
using Quartermaster.Server.Validation;

namespace Quartermaster.Server.ChapterAssociates;

public class ChapterOfficerSearchRequestValidator : Validator<ChapterOfficerSearchRequest> {
    public ChapterOfficerSearchRequestValidator() {
        this.AddPaginationRules();
    }
}
