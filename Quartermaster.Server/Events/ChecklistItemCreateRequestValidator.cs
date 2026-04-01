using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Events;

namespace Quartermaster.Server.Events;

public class ChecklistItemCreateRequestValidator : Validator<ChecklistItemCreateRequest> {
    public ChecklistItemCreateRequestValidator() {
        RuleFor(x => x.EventId)
            .NotEqual(System.Guid.Empty)
            .WithMessage("Event muss angegeben werden.");

        RuleFor(x => x.Label)
            .NotEmpty()
            .WithMessage("Bezeichnung darf nicht leer sein.")
            .MaximumLength(1024)
            .WithMessage("Bezeichnung darf maximal 1024 Zeichen lang sein.");

        RuleFor(x => x.ItemType)
            .InclusiveBetween(0, 2)
            .WithMessage("Ungültiger Checklistentyp.");
    }
}
