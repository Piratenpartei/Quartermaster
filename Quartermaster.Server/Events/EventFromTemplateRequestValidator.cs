using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Events;

namespace Quartermaster.Server.Events;

public class EventFromTemplateRequestValidator : Validator<EventFromTemplateRequest> {
    public EventFromTemplateRequestValidator() {
        RuleFor(x => x.TemplateId)
            .NotEqual(System.Guid.Empty)
            .WithMessage("Vorlage muss ausgewählt werden.");

        RuleFor(x => x.ChapterId)
            .NotEqual(System.Guid.Empty)
            .WithMessage("Gliederung muss ausgewählt werden.");
    }
}
