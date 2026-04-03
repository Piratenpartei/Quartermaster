using FastEndpoints;
using Quartermaster.Api.Chapters;
using Quartermaster.Server.Validation;

namespace Quartermaster.Server.Chapters;

public class ChapterSearchRequestValidator : Validator<ChapterSearchRequest> {
    public ChapterSearchRequestValidator() {
        this.AddPaginationRules();
    }
}
