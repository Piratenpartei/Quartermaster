using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Users;

namespace Quartermaster.Server.Users;

public class LoginRequestValidator : Validator<LoginRequest> {
    public LoginRequestValidator() {
        RuleFor(x => x.Username)
            .NotEmpty()
            .When(x => string.IsNullOrEmpty(x.EMail))
            .WithMessage("Benutzername oder E-Mail muss angegeben werden.");

        RuleFor(x => x.EMail)
            .NotEmpty()
            .When(x => string.IsNullOrEmpty(x.Username))
            .WithMessage("Benutzername oder E-Mail muss angegeben werden.");

        RuleFor(x => x.Password)
            .MinimumLength(12)
            .WithMessage("Das Passwort muss mindestens 12 Zeichen lang sein.");
    }
}
