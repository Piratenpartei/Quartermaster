using System.Linq;
using FluentValidation.TestHelper;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Server.AssemblyMotions.Endpoints.Members;

namespace Quartermaster.Server.Tests.AssemblyMotions;

public class AssemblyMotionSaveRequestValidatorTests {
    private readonly AssemblyMotionSaveRequestValidator _validator = new();

    private static AssemblyMotionSaveRequest ValidRequest() => new() {
        Title = "Titel",
        TextMarkdown = "Text",
        ReasonMarkdown = "Grund"
    };

    [Test]
    public void ValidRequest_ShouldHaveNoErrors() {
        var result = _validator.TestValidate(ValidRequest());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void EmptyTitle_ShouldHaveError() {
        var request = ValidRequest();
        request.Title = "";

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.TitleRequired);
    }

    [Test]
    public void TitleOver500_ShouldHaveError() {
        var request = ValidRequest();
        request.Title = new string('a', 501);

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.TitleMaxLength);
    }

    [Test]
    public void Title500_ShouldHaveNoError() {
        var request = ValidRequest();
        request.Title = new string('a', 500);

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Title);
    }

    [Test]
    public void SectionOver500_ShouldHaveError() {
        var request = ValidRequest();
        request.Section = new string('a', 501);

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Section)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.SectionMaxLength);
    }

    [Test]
    public void EmptyText_ShouldHaveError() {
        var request = ValidRequest();
        request.TextMarkdown = "";

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.TextMarkdown)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.TextRequired);
    }

    [Test]
    public void EmptyReason_ShouldHaveError() {
        var request = ValidRequest();
        request.ReasonMarkdown = "";

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.ReasonMarkdown)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.ReasonRequired);
    }

    [Test]
    public void SummaryOver2000_ShouldHaveError() {
        var request = ValidRequest();
        request.Summary = new string('a', 2001);

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Summary)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.SummaryMaxLength);
    }

    [Test]
    public void KeywordsOver500_ShouldHaveError() {
        var request = ValidRequest();
        request.Keywords = new string('a', 501);

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Keywords)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.KeywordsMaxLength);
    }

    [Test]
    public void PriorDiscussionOver2000_ShouldHaveError() {
        var request = ValidRequest();
        request.PriorDiscussion = new string('a', 2001);

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.PriorDiscussion)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.PriorDiscussionMaxLength);
    }

    [Test]
    public void RawHtmlInText_ShouldHaveError() {
        var request = ValidRequest();
        request.TextMarkdown = "<script>alert(1)</script>";

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.TextMarkdown)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.MarkdownHtmlNotAllowed);
    }

    [Test]
    public void RawHtmlInReason_ShouldHaveError() {
        var request = ValidRequest();
        request.ReasonMarkdown = "Grund <b>fett</b>";

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.ReasonMarkdown)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.MarkdownHtmlNotAllowed);
    }

    [Test]
    public void ImageInText_ShouldHaveError() {
        var request = ValidRequest();
        request.TextMarkdown = "![alt](https://example.org/a.png)";

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.TextMarkdown)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.MarkdownImageNotAllowed);
    }

    [Test]
    public void ImageInReason_ShouldHaveError() {
        var request = ValidRequest();
        request.ReasonMarkdown = "![alt](https://example.org/a.png)";

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.ReasonMarkdown)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.MarkdownImageNotAllowed);
    }

    [Test]
    public void DeeplyNestedText_ShouldHaveErrorWithoutThrowing() {
        var request = ValidRequest();
        request.TextMarkdown = string.Concat(Enumerable.Repeat("> - ", 60)) + "x";

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.TextMarkdown)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.MarkdownTooDeeplyNested);
    }

    [Test]
    public void DeeplyNestedReason_ShouldHaveErrorWithoutThrowing() {
        var request = ValidRequest();
        request.ReasonMarkdown = string.Concat(Enumerable.Repeat("[a", 200));

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.ReasonMarkdown)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.MarkdownTooDeeplyNested);
    }

    [Test]
    public void TextOverLengthLimit_ShouldHaveError() {
        var request = ValidRequest();
        request.TextMarkdown = new string('a', 100_001);

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.TextMarkdown)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.MarkdownTooLong);
    }

    [Test]
    public void TextAtLengthLimit_ShouldHaveNoErrors() {
        var request = ValidRequest();
        request.TextMarkdown = new string('a', 100_000);

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void NullTextAndReason_ShouldHaveRequiredErrorsWithoutThrowing() {
        var request = ValidRequest();
        request.TextMarkdown = null!;
        request.ReasonMarkdown = null!;

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.TextMarkdown)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.TextRequired);
        result.ShouldHaveValidationErrorFor(x => x.ReasonMarkdown)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.ReasonRequired);
    }

    [Test]
    public void TablesAndStrikethrough_ShouldHaveNoErrors() {
        var request = ValidRequest();
        request.TextMarkdown = "| a | b |\n|---|---|\n| 1 | 2 |\n\n~~alt~~";

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    [MethodDataSource(typeof(MotionMarkdownSafetyTests), nameof(MotionMarkdownSafetyTests.ShapeShiftingPayloads))]
    public void TextThatBecomesHtmlWhenNormalized_ShouldHaveError(string markdown) {
        var request = ValidRequest();
        request.TextMarkdown = markdown;
        request.ReasonMarkdown = markdown;

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.TextMarkdown)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.MarkdownHtmlNotAllowed);
        result.ShouldHaveValidationErrorFor(x => x.ReasonMarkdown)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.MarkdownHtmlNotAllowed);
    }

    [Test]
    [Arguments("table", I18nKey.Error.AssemblyMotion.MarkdownTableTooLarge)]
    [Arguments("brackets", I18nKey.Error.AssemblyMotion.MarkdownTooManyBrackets)]
    [Arguments("markers", I18nKey.Error.AssemblyMotion.MarkdownTooMuchInlineMarkup)]
    [Arguments("lines", I18nKey.Error.AssemblyMotion.MarkdownTooManyLines)]
    [Arguments("output", I18nKey.Error.AssemblyMotion.MarkdownOutputTooLarge)]
    [Arguments("expanding", I18nKey.Error.AssemblyMotion.MarkdownTooLong)]
    public void TextOverACostLimit_ShouldHaveItsOwnError(string kind, string expectedKey) {
        var request = ValidRequest();
        request.TextMarkdown = CostlyText(kind);
        request.ReasonMarkdown = CostlyText(kind);

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.TextMarkdown)
            .WithErrorMessage(expectedKey);
        result.ShouldHaveValidationErrorFor(x => x.ReasonMarkdown)
            .WithErrorMessage(expectedKey);
    }

    private static string CostlyText(string kind) => kind switch {
        "table" => Repeat("|a", 400) + "\n" + Repeat("|-", 400) + "\n" + Repeat("a|\n", 400),
        "brackets" => Repeat("[a](", 251),
        "markers" => Repeat("*a ", 1001),
        "lines" => Repeat("a\n", 5001),
        "output" => new string('&', 100_000),
        _ => new string('שּׁ', 40_000)
    };

    private static string Repeat(string unit, int count)
        => string.Concat(Enumerable.Repeat(unit, count));
}
