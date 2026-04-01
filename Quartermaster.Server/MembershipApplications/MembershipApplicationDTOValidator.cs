using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.MembershipApplications;

namespace Quartermaster.Server.MembershipApplications;

public class MembershipApplicationDTOValidator : Validator<MembershipApplicationDTO> {
    public MembershipApplicationDTOValidator() {
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .WithMessage("Vorname darf nicht leer sein.")
            .MaximumLength(256)
            .WithMessage("Vorname darf maximal 256 Zeichen lang sein.");

        RuleFor(x => x.LastName)
            .NotEmpty()
            .WithMessage("Nachname darf nicht leer sein.")
            .MaximumLength(256)
            .WithMessage("Nachname darf maximal 256 Zeichen lang sein.");

        RuleFor(x => x.EMail)
            .NotEmpty()
            .WithMessage("E-Mail-Adresse darf nicht leer sein.")
            .Must(e => e != null && e.Contains('@'))
            .WithMessage("E-Mail-Adresse muss ein @ enthalten.")
            .MaximumLength(256)
            .WithMessage("E-Mail-Adresse darf maximal 256 Zeichen lang sein.");

        RuleFor(x => x.Citizenship)
            .NotEmpty()
            .WithMessage("Staatsangehörigkeit darf nicht leer sein.")
            .MaximumLength(256)
            .WithMessage("Staatsangehörigkeit darf maximal 256 Zeichen lang sein.");

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(64)
            .WithMessage("Telefonnummer darf maximal 64 Zeichen lang sein.");

        RuleFor(x => x.AddressStreet)
            .NotEmpty()
            .WithMessage("Straße darf nicht leer sein.")
            .MaximumLength(256)
            .WithMessage("Straße darf maximal 256 Zeichen lang sein.");

        RuleFor(x => x.AddressHouseNbr)
            .NotEmpty()
            .WithMessage("Hausnummer darf nicht leer sein.")
            .MaximumLength(32)
            .WithMessage("Hausnummer darf maximal 32 Zeichen lang sein.");

        RuleFor(x => x.AddressPostCode)
            .NotEmpty()
            .WithMessage("Postleitzahl darf nicht leer sein.")
            .MaximumLength(16)
            .WithMessage("Postleitzahl darf maximal 16 Zeichen lang sein.");

        RuleFor(x => x.AddressCity)
            .NotEmpty()
            .WithMessage("Stadt darf nicht leer sein.")
            .MaximumLength(256)
            .WithMessage("Stadt darf maximal 256 Zeichen lang sein.");

        RuleFor(x => x.ApplicationText)
            .MaximumLength(2048)
            .WithMessage("Antragstext darf maximal 2048 Zeichen lang sein.");

        RuleFor(x => x.ConformityDeclarationAccepted)
            .Equal(true)
            .WithMessage("Die Grundsatzerklärung muss akzeptiert werden.");
    }
}
