using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Meetings;

namespace Quartermaster.Server.Meetings.Validators;

public class MeetingUpdateRequestValidator : Validator<MeetingUpdateRequest> {
    public MeetingUpdateRequestValidator() {
        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("Titel darf nicht leer sein.")
            .MaximumLength(200)
            .WithMessage("Titel darf maximal 200 Zeichen lang sein.");

        RuleFor(x => x.Location)
            .MaximumLength(500)
            .WithMessage("Ort darf maximal 500 Zeichen lang sein.");

        RuleFor(x => x.Description)
            .MaximumLength(10000)
            .WithMessage("Beschreibung darf maximal 10000 Zeichen lang sein.");

        RuleFor(x => x.Visibility)
            .IsInEnum()
            .WithMessage("Ungültige Sichtbarkeit.");
    }
}
