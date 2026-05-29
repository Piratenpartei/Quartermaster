using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Motions;

namespace Quartermaster.Server.Motions;

public class MotionUpdateRequestValidator : Validator<MotionUpdateRequest> {
    public MotionUpdateRequestValidator() {
        RuleFor(x => x.AuthorName)
            .NotEmpty()
            .WithMessage(I18nKey.Error.Motion.SubmitterNameRequired)
            .MaximumLength(256)
            .WithMessage(I18nKey.Error.Motion.SubmitterNameMaxLength);

        RuleFor(x => x.AuthorEmail)
            .NotEmpty()
            .WithMessage(I18nKey.Error.Motion.EmailRequired)
            .Must(e => e != null && e.Contains('@'))
            .WithMessage(I18nKey.Error.Motion.EmailInvalid)
            .MaximumLength(256)
            .WithMessage(I18nKey.Error.Motion.EmailMaxLength);

        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage(I18nKey.Error.Motion.TitleRequired)
            .MaximumLength(512)
            .WithMessage(I18nKey.Error.Motion.TitleMaxLength);

        RuleFor(x => x.TextMarkdown)
            .NotEmpty()
            .WithMessage(I18nKey.Error.Motion.BodyRequired)
            .MaximumLength(8192)
            .WithMessage(I18nKey.Error.Motion.BodyMaxLength);
    }
}
