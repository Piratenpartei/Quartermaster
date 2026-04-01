using FastEndpoints;
using FluentValidation;

namespace Quartermaster.Server.Events;

public class ChecklistItemReorderRequestValidator : Validator<ChecklistItemReorderRequest> {
    public ChecklistItemReorderRequestValidator() {
        RuleFor(x => x.EventId)
            .NotEqual(System.Guid.Empty)
            .WithMessage("Event muss angegeben werden.");

        RuleFor(x => x.ItemId)
            .NotEqual(System.Guid.Empty)
            .WithMessage("Element-ID darf nicht leer sein.");

        RuleFor(x => x.Direction)
            .Must(d => d == -1 || d == 1)
            .WithMessage("Richtung muss -1 (hoch) oder 1 (runter) sein.");
    }
}
