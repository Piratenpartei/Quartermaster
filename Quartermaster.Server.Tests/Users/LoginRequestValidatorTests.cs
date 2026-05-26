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
            Email = null,
            Password = "SecurePass123!"
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void ValidRequest_EmailOnly_ShouldHaveNoErrors() {
        var request = new LoginRequest {
            Username = null,
            Email = "test@example.com",
            Password = "SecurePass123!"
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void ValidRequest_BothUsernameAndEmail_ShouldHaveNoErrors() {
        var request = new LoginRequest {
            Username = "testuser",
            Email = "test@example.com",
            Password = "SecurePass123!"
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void NeitherUsernameNorEmail_ShouldHaveErrors() {
        var request = new LoginRequest {
            Username = null,
            Email = null,
            Password = "SecurePass123!"
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Username)
            .WithErrorMessage(I18nKey.Error.User.Login.UsernameOrEmailRequired);
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage(I18nKey.Error.User.Login.UsernameOrEmailRequired);
    }

    [Test]
    public void EmptyUsernameAndEmptyEmail_ShouldHaveErrors() {
        var request = new LoginRequest {
            Username = "",
            Email = "",
            Password = "SecurePass123!"
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Username)
            .WithErrorMessage(I18nKey.Error.User.Login.UsernameOrEmailRequired);
        result.ShouldHaveValidationErrorFor(x => x.Email)
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
