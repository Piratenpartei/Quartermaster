using FluentValidation.TestHelper;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.I18n;
using Quartermaster.Server.Chapters;

namespace Quartermaster.Server.Tests.Chapters;

public class ChapterSearchRequestValidatorTests {
    private readonly ChapterSearchRequestValidator _validator = new();

    [Test]
    public void Defaults_pass() {
        var result = _validator.TestValidate(new ChapterSearchRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void Page_zero_errors() {
        var result = _validator.TestValidate(new ChapterSearchRequest { Page = 0, PageSize = 25 });
        result.ShouldHaveValidationErrorFor(x => x.Page)
            .WithErrorMessage(I18nKey.Error.Validation.PageMin);
    }

    [Test]
    public void PageSize_zero_errors() {
        var result = _validator.TestValidate(new ChapterSearchRequest { Page = 1, PageSize = 0 });
        result.ShouldHaveValidationErrorFor(x => x.PageSize)
            .WithErrorMessage(I18nKey.Error.Validation.PageSizeRange);
    }

    [Test]
    public void PageSize_101_errors() {
        var result = _validator.TestValidate(new ChapterSearchRequest { Page = 1, PageSize = 101 });
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }
}
