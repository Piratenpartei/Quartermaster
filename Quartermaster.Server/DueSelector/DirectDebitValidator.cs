using FluentValidation;
using Quartermaster.Api.Banking;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.I18n;

namespace Quartermaster.Server.DueSelector;

/// <summary>
/// Direct debit needs an account holder and an IBAN that passes the offline check. A plain FluentValidation validator, so
/// FastEndpoints doesn't register it for <see cref="DueSelectionDTO"/> next to <see cref="DueSelectionDTOValidator"/>;
/// both that one and the membership application validator include it.
/// </summary>
public class DirectDebitValidator : AbstractValidator<DueSelectionDTO> {
    public DirectDebitValidator() {
        When(x => x.IsDirectDeposit, () => {
            RuleFor(x => x.AccountHolder)
                .NotEmpty()
                .WithMessage(I18nKey.Error.Admin.DueSelection.AccountHolderRequired);

            RuleFor(x => x.IBAN)
                .Must(Iban.IsValid)
                .WithMessage(I18nKey.Error.Admin.DueSelection.IbanInvalid);
        });
    }
}
