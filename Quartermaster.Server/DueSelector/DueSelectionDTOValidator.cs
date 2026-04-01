using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.DueSelector;

namespace Quartermaster.Server.DueSelector;

public class DueSelectionDTOValidator : Validator<DueSelectionDTO> {
    public DueSelectionDTOValidator() {
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .WithMessage("Vorname darf nicht leer sein.");

        RuleFor(x => x.LastName)
            .NotEmpty()
            .WithMessage("Nachname darf nicht leer sein.");

        RuleFor(x => x.EMail)
            .Must(e => string.IsNullOrEmpty(e) || e.Contains('@'))
            .WithMessage("E-Mail-Adresse muss ein @ enthalten.");

        RuleFor(x => x.SelectedDue)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Beitrag darf nicht negativ sein.");

        RuleFor(x => x.AccountHolder)
            .MaximumLength(256)
            .WithMessage("Kontoinhaber darf maximal 256 Zeichen lang sein.");

        RuleFor(x => x.IBAN)
            .MaximumLength(64)
            .WithMessage("IBAN darf maximal 64 Zeichen lang sein.");

        RuleFor(x => x.ReducedJustification)
            .MaximumLength(2048)
            .WithMessage("Begründung darf maximal 2048 Zeichen lang sein.");
    }
}
