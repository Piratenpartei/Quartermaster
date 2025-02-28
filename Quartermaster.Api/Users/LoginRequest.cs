//using FastEndpoints;
//using FluentValidation;

namespace Quartermaster.Api.Users;

public class LoginRequest {
    public string? Username { get; set; }
    public string? EMail { get; set; }
    public required string Password { get; set; }
}

//public class LoginRequestValidator : Validator<LoginRequest> {
//    public LoginRequestValidator() {
//        RuleFor(m => m.Username)
//            .NotEmpty().When(m => string.IsNullOrEmpty(m.EMail))
//            .WithMessage("Benutzername oder EMail muss angegeben werden.");

//        RuleFor(m => m.EMail)
//            .NotEmpty().When(m => string.IsNullOrEmpty(m.Username))
//            .WithMessage("Benutzername oder EMail muss angegeben werden.");

//        RuleFor(m => m.Password)
//            .MinimumLength(12)
//            .WithMessage("Das Password muss mindestens 12 Zeichen lang sein.");
//    }
//}