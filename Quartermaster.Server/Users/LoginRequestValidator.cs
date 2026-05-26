using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Users;

namespace Quartermaster.Server.Users;

public class LoginRequestValidator : Validator<LoginRequest> {
    public LoginRequestValidator() {
        RuleFor(x => x.Username)
            .NotEmpty()
            .When(x => string.IsNullOrEmpty(x.Email))
            .WithMessage(I18nKey.Error.User.Login.UsernameOrEmailRequired);

        RuleFor(x => x.Email)
            .NotEmpty()
            .When(x => string.IsNullOrEmpty(x.Username))
            .WithMessage(I18nKey.Error.User.Login.UsernameOrEmailRequired);

        RuleFor(x => x.Password)
            .MinimumLength(12)
            .WithMessage(I18nKey.Error.User.Login.PasswordMinLength);
    }
}
