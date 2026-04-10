using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Options;

namespace Quartermaster.Server.Options;

public class OptionUpdateRequestValidator : Validator<OptionUpdateRequest> {
    public OptionUpdateRequestValidator() {
        RuleFor(x => x.Identifier)
            .NotEmpty()
            .WithMessage(I18nKey.Error.Admin.Option.IdentifierRequired);

        RuleFor(x => x.Value)
            .MaximumLength(8192)
            .WithMessage(I18nKey.Error.Admin.Option.ValueMaxLength);
    }
}
