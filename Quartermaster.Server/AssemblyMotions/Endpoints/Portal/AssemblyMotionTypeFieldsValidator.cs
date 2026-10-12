using System.Linq;
using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Portal;

public abstract class AssemblyMotionTypeFieldsValidator<T> : Validator<T> where T : AssemblyMotionTypeFields {
    protected AssemblyMotionTypeFieldsValidator() {
        RuleFor(x => x.Code)
            .Must(IsValidCode)
            .WithMessage(I18nKey.Error.AssemblyMotion.Type.CodeInvalid);

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage(I18nKey.Error.AssemblyMotion.Type.NameRequired);

        RuleFor(x => x.MinSubmitters)
            .GreaterThanOrEqualTo(1)
            .WithMessage(I18nKey.Error.AssemblyMotion.Type.MinSubmittersInvalid);

        When(x => x.Deadline != null && x.CompetingDeadline != null, () => {
            RuleFor(x => x.CompetingDeadline)
                .GreaterThan(x => x.Deadline)
                .WithMessage(I18nKey.Error.AssemblyMotion.Type.CompetingDeadlineInvalid);
        });
    }

    private static bool IsValidCode(string? code) {
        var trimmed = code?.Trim() ?? "";
        if (trimmed.Length < 1 || trimmed.Length > 16)
            return false;
        return trimmed.All(char.IsLetter);
    }
}
