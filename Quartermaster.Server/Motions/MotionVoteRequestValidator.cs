using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Motions;

namespace Quartermaster.Server.Motions;

public class MotionVoteRequestValidator : Validator<MotionVoteRequest> {
    public MotionVoteRequestValidator() {
        RuleFor(x => x.MotionId)
            .NotEqual(System.Guid.Empty)
            .WithMessage("Antrags-ID darf nicht leer sein.");

        RuleFor(x => x.UserId)
            .NotEqual(System.Guid.Empty)
            .WithMessage("Benutzer-ID darf nicht leer sein.");

        RuleFor(x => x.Vote)
            .InclusiveBetween(0, 2)
            .WithMessage("Ungültige Abstimmung.");
    }
}
