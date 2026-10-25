using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.I18n;

namespace Quartermaster.Server.DueSelector;

public class DueSelectionDTOValidator : Validator<DueSelectionDTO> {
    public DueSelectionDTOValidator() {
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .WithMessage(I18nKey.Error.Admin.DueSelection.FirstNameRequired);

        RuleFor(x => x.LastName)
            .NotEmpty()
            .WithMessage(I18nKey.Error.Admin.DueSelection.LastNameRequired);

        RuleFor(x => x.Email)
            .Must(e => string.IsNullOrEmpty(e) || e.Contains('@'))
            .WithMessage(I18nKey.Error.Admin.DueSelection.EmailInvalid);

        Include(new DueSelectionRulesValidator());
    }
}
