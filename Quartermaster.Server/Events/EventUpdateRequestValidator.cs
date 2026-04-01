using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Events;

namespace Quartermaster.Server.Events;

public class EventUpdateRequestValidator : Validator<EventUpdateRequest> {
    public EventUpdateRequestValidator() {
        RuleFor(x => x.Id)
            .NotEqual(System.Guid.Empty)
            .WithMessage("Event-ID darf nicht leer sein.");

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
