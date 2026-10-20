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

        RuleFor(x => x.SelectedDue)
            .GreaterThanOrEqualTo(0)
            .WithMessage(I18nKey.Error.Admin.DueSelection.AmountNotNegative);

        // The browser enforces the same rates; without this check a low amount under a non-reduced valuation would skip the reduction decision.
        RuleFor(x => x.SelectedDue)
            .Must((dto, due) => due >= DueSelectionRates.MinimumDue(dto))
            .WithMessage(I18nKey.Error.Admin.DueSelection.DueBelowRate);

        RuleFor(x => x.AccountHolder)
            .MaximumLength(256)
            .WithMessage(I18nKey.Error.Admin.DueSelection.AccountHolderMaxLength);

        RuleFor(x => x.IBAN)
            .MaximumLength(64)
            .WithMessage(I18nKey.Error.Admin.DueSelection.IbanMaxLength);

        Include(new DirectDebitValidator());

        RuleFor(x => x.ReducedJustification)
            .MaximumLength(2048)
            .WithMessage(I18nKey.Error.Admin.DueSelection.JustificationMaxLength);
    }
}
