using FluentValidation.TestHelper;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Users;
using Quartermaster.Server.Users;

namespace Quartermaster.Server.Tests.Users;

public class LoginRequestValidatorTests {
    private readonly LoginRequestValidator _validator = new();

    [Test]
    public void ValidRequest_UsernameOnly_ShouldHaveNoErrors() {
        var request = new LoginRequest {
            Username = "testuser",
            EMail = null,
            Password = "SecurePass123!"
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void ValidRequest_EMailOnly_ShouldHaveNoErrors() {
        var request = new LoginRequest {
            Username = null,
            EMail = "test@example.com",
            Password = "SecurePass123!"
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void ValidRequest_BothUsernameAndEMail_ShouldHaveNoErrors() {
        var request = new LoginRequest {
            Username = "testuser",
            EMail = "test@example.com",
            Password = "SecurePass123!"
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void NeitherUsernameNorEMail_ShouldHaveErrors() {
        var request = new LoginRequest {
            Username = null,
            EMail = null,
            Password = "SecurePass123!"
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Username)
            .WithErrorMessage(I18nKey.Error.User.Login.UsernameOrEmailRequired);
        result.ShouldHaveValidationErrorFor(x => x.EMail)
            .WithErrorMessage(I18nKey.Error.User.Login.UsernameOrEmailRequired);
    }

    [Test]
    public void EmptyUsernameAndEmptyEMail_ShouldHaveErrors() {
        var request = new LoginRequest {
            Username = "",
            EMail = "",
            Password = "SecurePass123!"
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Username)
            .WithErrorMessage(I18nKey.Error.User.Login.UsernameOrEmailRequired);
        result.ShouldHaveValidationErrorFor(x => x.EMail)
            .WithErrorMessage(I18nKey.Error.User.Login.UsernameOrEmailRequired);
    }

    [Test]
    public void PasswordTooShort_ShouldHaveError() {
        var request = new LoginRequest {
            Username = "testuser",
            Password = "Short123!"
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage(I18nKey.Error.User.Login.PasswordMinLength);
    }

    [Test]
    public void PasswordAtMinLength_ShouldHaveNoError() {
        var request = new LoginRequest {
            Username = "testuser",
            Password = "123456789012"
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    [Test]
    public void PasswordExactlyElevenChars_ShouldHaveError() {
        var request = new LoginRequest {
            Username = "testuser",
            Password = "12345678901"
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage(I18nKey.Error.User.Login.PasswordMinLength);
    }
}
