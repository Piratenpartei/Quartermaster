using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Motions;

namespace Quartermaster.Server.Motions;

public class MotionCreateRequestValidator : Validator<MotionCreateRequest> {
    public MotionCreateRequestValidator() {
        RuleFor(x => x.ChapterId)
            .NotEqual(System.Guid.Empty)
            .WithMessage("Gliederung muss ausgewählt werden.");

        RuleFor(x => x.AuthorName)
            .NotEmpty()
            .WithMessage("Name des Antragstellers darf nicht leer sein.")
            .MaximumLength(256)
            .WithMessage("Name des Antragstellers darf maximal 256 Zeichen lang sein.");

        RuleFor(x => x.AuthorEMail)
            .NotEmpty()
            .WithMessage("E-Mail-Adresse darf nicht leer sein.")
            .Must(e => e != null && e.Contains('@'))
            .WithMessage("E-Mail-Adresse muss ein @ enthalten.")
            .MaximumLength(256)
            .WithMessage("E-Mail-Adresse darf maximal 256 Zeichen lang sein.");

        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("Titel darf nicht leer sein.")
            .MaximumLength(512)
            .WithMessage("Titel darf maximal 512 Zeichen lang sein.");

        RuleFor(x => x.Text)
            .NotEmpty()
            .WithMessage("Antragstext darf nicht leer sein.")
            .MaximumLength(8192)
            .WithMessage("Antragstext darf maximal 8192 Zeichen lang sein.");
    }
}
