using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Events;

namespace Quartermaster.Server.Events;

public class EventCreateRequestValidator : Validator<EventCreateRequest> {
    public EventCreateRequestValidator() {
        RuleFor(x => x.ChapterId)
            .NotEqual(System.Guid.Empty)
            .WithMessage("Gliederung muss ausgewählt werden.");

        RuleFor(x => x.InternalName)
            .NotEmpty()
            .WithMessage("Interner Name darf nicht leer sein.")
            .MaximumLength(512)
            .WithMessage("Interner Name darf maximal 512 Zeichen lang sein.");

        RuleFor(x => x.PublicName)
            .NotEmpty()
            .WithMessage("Öffentlicher Name darf nicht leer sein.")
            .MaximumLength(512)
            .WithMessage("Öffentlicher Name darf maximal 512 Zeichen lang sein.");
    }
}
