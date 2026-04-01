using FastEndpoints;
using FluentValidation;

namespace Quartermaster.Server.Admin;

public class MembershipApplicationProcessRequestValidator : Validator<MembershipApplicationProcessRequest> {
    public MembershipApplicationProcessRequestValidator() {
        RuleFor(x => x.Id)
            .NotEqual(System.Guid.Empty)
            .WithMessage("Antrags-ID darf nicht leer sein.");

        RuleFor(x => x.Status)
            .InclusiveBetween(1, 2)
            .WithMessage("Status muss 'Genehmigt' oder 'Abgelehnt' sein.");
    }
}
