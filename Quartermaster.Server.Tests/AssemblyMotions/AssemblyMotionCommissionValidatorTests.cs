using FluentValidation.TestHelper;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Server.AssemblyMotions.Endpoints.Commission;

namespace Quartermaster.Server.Tests.AssemblyMotions;

public class AssemblyMotionCheckRequestValidatorTests {
    private readonly AssemblyMotionCheckRequestValidator _validator = new();

    [Test]
    public void ValidRequest_ShouldHaveNoErrors() {
        var request = new AssemblyMotionCheckRequest { Status = AssemblyMotionStatus.Admitted, CheckNote = new string('a', 4000) };

        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void CheckNoteOver4000_ShouldHaveError() {
        var request = new AssemblyMotionCheckRequest { Status = AssemblyMotionStatus.Admitted, CheckNote = new string('a', 4001) };

        _validator.TestValidate(request)
            .ShouldHaveValidationErrorFor(x => x.CheckNote)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.CheckNoteMaxLength);
    }

    [Test]
    public void UndefinedStatus_ShouldHaveError() {
        var request = new AssemblyMotionCheckRequest { Status = (AssemblyMotionStatus)99 };

        _validator.TestValidate(request)
            .ShouldHaveValidationErrorFor(x => x.Status)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.InvalidState);
    }
}

public class AssemblyMotionCommissionEditRequestValidatorTests {
    private readonly AssemblyMotionCommissionEditRequestValidator _validator = new();

    private static AssemblyMotionCommissionEditRequest ValidRequest() => new() {
        Title = "Titel",
        TextMarkdown = "Text",
        ReasonMarkdown = "Grund",
        ChangeNote = new string('a', 2000)
    };

    [Test]
    public void ValidRequest_ShouldHaveNoErrors() {
        _validator.TestValidate(ValidRequest()).ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void ChangeNoteOver2000_ShouldHaveError() {
        var request = ValidRequest();
        request.ChangeNote = new string('a', 2001);

        _validator.TestValidate(request)
            .ShouldHaveValidationErrorFor(x => x.ChangeNote)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.ChangeNoteMaxLength);
    }

    [Test]
    public void RawHtmlInText_ShouldHaveError() {
        var request = ValidRequest();
        request.TextMarkdown = "<b>x</b>";

        _validator.TestValidate(request)
            .ShouldHaveValidationErrorFor(x => x.TextMarkdown)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.MarkdownHtmlNotAllowed);
    }
}
