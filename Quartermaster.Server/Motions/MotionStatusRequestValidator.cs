using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Motions;

namespace Quartermaster.Server.Motions;

public class MotionStatusRequestValidator : Validator<MotionStatusRequest> {
    public MotionStatusRequestValidator() {
        RuleFor(x => x.MotionId)
            .NotEqual(System.Guid.Empty)
            .WithMessage(I18nKey.Error.Motion.Status.MotionIdRequired);
    }
}
