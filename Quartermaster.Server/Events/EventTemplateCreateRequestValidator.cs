using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Events;

namespace Quartermaster.Server.Events;

public class EventTemplateCreateRequestValidator : Validator<EventTemplateCreateRequest> {
    public EventTemplateCreateRequestValidator() {
        RuleFor(x => x.EventId)
            .NotEqual(System.Guid.Empty)
            .WithMessage("Event muss angegeben werden.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Vorlagenname darf nicht leer sein.")
            .MaximumLength(512)
            .WithMessage("Vorlagenname darf maximal 512 Zeichen lang sein.");
    }
}
