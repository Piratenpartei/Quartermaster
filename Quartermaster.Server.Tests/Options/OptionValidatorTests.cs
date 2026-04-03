using FluentValidation.TestHelper;
using Quartermaster.Api.Options;
using Quartermaster.Server.Options;

namespace Quartermaster.Server.Tests.Options;

public class OptionUpdateRequestValidatorTests {
    private readonly OptionUpdateRequestValidator _validator = new();

    [Test]
    public void ValidRequest_ShouldHaveNoErrors() {
        var request = new OptionUpdateRequest {
            Identifier = "some.setting",
            Value = "some value"
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void EmptyIdentifier_ShouldHaveError() {
        var request = new OptionUpdateRequest {
            Identifier = "",
            Value = "some value"
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Identifier)
            .WithErrorMessage("Bezeichner darf nicht leer sein.");
    }

    [Test]
    public void ValueExceedsMaxLength_ShouldHaveError() {
        var request = new OptionUpdateRequest {
            Identifier = "some.setting",
            Value = new string('A', 8193)
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Value)
            .WithErrorMessage("Wert darf maximal 8192 Zeichen lang sein.");
    }

    [Test]
    public void ValueAtMaxLength_ShouldHaveNoError() {
        var request = new OptionUpdateRequest {
            Identifier = "some.setting",
            Value = new string('A', 8192)
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Value);
    }

    [Test]
    public void EmptyValue_ShouldHaveNoError() {
        var request = new OptionUpdateRequest {
            Identifier = "some.setting",
            Value = ""
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Value);
    }
}
