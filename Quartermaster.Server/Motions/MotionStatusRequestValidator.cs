using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Motions;

namespace Quartermaster.Server.Motions;

public class MotionStatusRequestValidator : Validator<MotionStatusRequest> {
    public MotionStatusRequestValidator() {
        RuleFor(x => x.MotionId)
            .NotEqual(System.Guid.Empty)
            .WithMessage("Antrags-ID darf nicht leer sein.");
    }
}
