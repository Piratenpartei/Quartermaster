using FastEndpoints;
using FluentValidation;

namespace Quartermaster.Server.Admin;

public class DueSelectionProcessRequestValidator : Validator<DueSelectionProcessRequest> {
    public DueSelectionProcessRequestValidator() {
        RuleFor(x => x.Id)
            .NotEqual(System.Guid.Empty)
            .WithMessage("Beitragsauswahl-ID darf nicht leer sein.");

        RuleFor(x => x.Status)
            .InclusiveBetween(1, 2)
            .WithMessage("Status muss 'Genehmigt' oder 'Abgelehnt' sein.");
    }
}
