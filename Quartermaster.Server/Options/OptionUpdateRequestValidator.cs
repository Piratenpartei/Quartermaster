using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Options;

namespace Quartermaster.Server.Options;

public class OptionUpdateRequestValidator : Validator<OptionUpdateRequest> {
    public OptionUpdateRequestValidator() {
        RuleFor(x => x.Identifier)
            .NotEmpty()
            .WithMessage("Bezeichner darf nicht leer sein.");

        RuleFor(x => x.Value)
            .MaximumLength(8192)
            .WithMessage("Wert darf maximal 8192 Zeichen lang sein.");
    }
}
