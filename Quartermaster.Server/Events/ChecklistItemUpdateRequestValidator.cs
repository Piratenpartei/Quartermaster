using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Events;

namespace Quartermaster.Server.Events;

public class ChecklistItemUpdateRequestValidator : Validator<ChecklistItemUpdateRequest> {
    public ChecklistItemUpdateRequestValidator() {
        RuleFor(x => x.EventId)
            .NotEqual(System.Guid.Empty)
            .WithMessage("Event muss angegeben werden.");

        RuleFor(x => x.ItemId)
            .NotEqual(System.Guid.Empty)
            .WithMessage("Element-ID darf nicht leer sein.");

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
