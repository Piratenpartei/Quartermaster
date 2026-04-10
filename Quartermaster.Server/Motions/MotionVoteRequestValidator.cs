using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Motions;

namespace Quartermaster.Server.Motions;

public class MotionVoteRequestValidator : Validator<MotionVoteRequest> {
    public MotionVoteRequestValidator() {
        RuleFor(x => x.MotionId)
            .NotEqual(System.Guid.Empty)
            .WithMessage(I18nKey.Error.Motion.Vote.MotionIdRequired);

        RuleFor(x => x.UserId)
            .NotEqual(System.Guid.Empty)
            .WithMessage(I18nKey.Error.Motion.Vote.UserIdRequired);

        RuleFor(x => x.Vote)
            .InclusiveBetween(0, 2)
            .WithMessage(I18nKey.Error.Motion.Vote.InvalidVote);
    }
}
