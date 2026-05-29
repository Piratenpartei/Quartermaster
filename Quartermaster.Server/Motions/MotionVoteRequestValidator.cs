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

        RuleFor(x => x.MemberId)
            .NotEqual(System.Guid.Empty)
            .WithMessage(I18nKey.Error.Motion.Vote.MemberIdRequired);

        RuleFor(x => x.Vote)
            .IsInEnum()
            .WithMessage(I18nKey.Error.Motion.Vote.InvalidVote);
    }
}
